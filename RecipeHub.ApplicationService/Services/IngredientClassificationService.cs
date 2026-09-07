using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RecipeHub.Domain.Entities.Recipe;

namespace RecipeHub.ApplicationService.Services
{
    public sealed class IngredientClassificationService : IIngredientClassificationService
    {
        private readonly HttpClient _httpClient;

        private static readonly Dictionary<string, string[]> LocalCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            ["produce"] = new[] { "apple", "avocado", "banana", "bean", "berry", "broccoli", "cabbage", "carrot", "celery", "cucumber", "garlic", "grape", "kale", "lemon", "lettuce", "lime", "mango", "mushroom", "onion", "orange", "pea", "pepper", "potato", "spinach", "tomato", "zucchini" },
            ["meat"] = new[] { "bacon", "beef", "chicken", "duck", "ham", "lamb", "pork", "sausage", "turkey", "veal" },
            ["seafood"] = new[] { "anchovy", "cod", "crab", "fish", "mussel", "salmon", "shrimp", "prawn", "tuna" },
            ["dairy"] = new[] { "butter", "cheese", "cream", "milk", "mozzarella", "parmesan", "yogurt", "yoghurt" },
            ["grains"] = new[] { "bread", "cereal", "couscous", "flour", "noodle", "oat", "pasta", "quinoa", "rice", "tortilla" },
            ["legumes"] = new[] { "chickpea", "lentil", "soy", "split pea" },
            ["nutsAndSeeds"] = new[] { "almond", "cashew", "chia", "hazelnut", "peanut", "pecan", "pistachio", "sesame", "walnut" },
            ["beverages"] = new[] { "coffee", "juice", "tea", "water", "wine" },
            ["baking"] = new[] { "baking powder", "baking soda", "cocoa", "sugar", "vanilla", "yeast" },
            ["condiments"] = new[] { "broth", "ketchup", "mayonnaise", "mustard", "sauce", "stock", "vinegar" },
            ["herbsAndSpices"] = new[] { "basil", "cinnamon", "cumin", "curry", "dill", "italian seasoning", "mint", "oregano", "paprika", "parsley", "rosemary", "salt", "thyme", "turmeric" },
            ["oilsAndFats"] = new[] { "oil", "lard", "margarine" }
        };

        public IngredientClassificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IngredientClassificationResult> ClassifyAsync(string name)
        {
            var normalizedName = (name ?? string.Empty).Trim();
            var local = ClassifyLocally(normalizedName);
            if (local.Category != "other")
                return local;

            try
            {
                using var response = await _httpClient.GetAsync($"cgi/search.pl?search_terms={Uri.EscapeDataString(normalizedName)}&search_simple=1&action=process&json=1&page_size=1");
                if (!response.IsSuccessStatusCode)
                    return local;

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var product = document.RootElement.TryGetProperty("products", out var products) && products.GetArrayLength() > 0
                    ? products[0]
                    : default;
                if (product.ValueKind == JsonValueKind.Undefined)
                    return local;

                var tags = product.TryGetProperty("categories_tags", out var categoryTags)
                    ? categoryTags.EnumerateArray().Select(tag => tag.GetString() ?? string.Empty).ToList()
                    : new List<string>();
                var category = MapOpenFoodFactsCategory(tags);
                var canonicalName = product.TryGetProperty("product_name", out var productName) && !string.IsNullOrWhiteSpace(productName.GetString())
                    ? productName.GetString()
                    : normalizedName;
                var productId = product.TryGetProperty("code", out var code) ? code.GetString() : null;

                return new IngredientClassificationResult
                {
                    CanonicalName = canonicalName,
                    Category = category,
                    Subcategory = tags.LastOrDefault(tag => tag.StartsWith("en:", StringComparison.OrdinalIgnoreCase))?.Substring(3) ?? "other",
                    OpenFoodFactsId = productId
                };
            }
            catch
            {
                return local;
            }
        }

        private static IngredientClassificationResult ClassifyLocally(string name)
        {
            var value = name.ToLowerInvariant();
            var category = LocalCategories.FirstOrDefault(item => item.Value.Any(term => value.Contains(term))).Key ?? "other";
            return new IngredientClassificationResult
            {
                CanonicalName = name,
                Category = category,
                Subcategory = "other"
            };
        }

        private static string MapOpenFoodFactsCategory(IEnumerable<string> tags)
        {
            var value = string.Join('|', tags).ToLowerInvariant();
            if (value.Contains("fruit") || value.Contains("vegetable") || value.Contains("plant-based")) return "produce";
            if (value.Contains("dairy") || value.Contains("milk")) return "dairy";
            if (value.Contains("seafood") || value.Contains("fish") || value.Contains("shellfish")) return "seafood";
            if (value.Contains("meat") || value.Contains("poultry")) return "meat";
            if (value.Contains("cereal") || value.Contains("bread") || value.Contains("pasta")) return "grains";
            if (value.Contains("beverage") || value.Contains("drink")) return "beverages";
            return "other";
        }
    }
}
