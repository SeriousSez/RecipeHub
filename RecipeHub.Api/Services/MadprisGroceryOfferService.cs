using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RecipeHub.Infrastructure;

namespace RecipeHub.Api.Services
{
    public class MadprisGroceryOfferService : IGroceryProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MadprisGroceryOfferService> _logger;
        private readonly RecipeHubContext _context;
        private readonly IRecipeTranslationService _translationService;

        public MadprisGroceryOfferService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<MadprisGroceryOfferService> logger,
            RecipeHubContext context,
            IRecipeTranslationService translationService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _context = context;
            _translationService = translationService;
        }

        public string CountryCode => "DK";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["ShelfAtlas:ApiKey"]);

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var locationKey = $"{Math.Round(model.Latitude, 3)}:{Math.Round(model.Longitude, 3)}:{Math.Round(model.RadiusKm, 1)}";
            var categoryKey = string.Join("|", ingredients.Select(ingredient => $"{ingredient}:{GetCategory(model, ingredient)}"));
            var shoppingPreference = GetShoppingPreference(model);
            var cacheKey = $"groceryoffers:{locationKey}:{shoppingPreference}:{categoryKey.ToLowerInvariant()}";
            if (!model.ForceRefresh && _cache.TryGetValue(cacheKey, out GroceryOfferSearchResponse cached))
            {
                return cached;
            }

            var marketFilters = await GetMarketFiltersAsync(model.ForceRefresh);
            var storesTask = GetNearbyStoresAsync(model, locationKey);
            var approvedCategories = await GetApprovedCategoriesAsync(ingredients);
            var translatedIngredients = await _translationService.TranslateIngredientNamesAsync(ingredients, "Danish", model.IngredientContexts)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var searches = ingredients.Select(ingredient =>
            {
                var category = GetCategory(model, ingredient);
                var categories = category == "auto" && approvedCategories.TryGetValue(NormalizeFeedbackValue(ingredient), out var approvedCategoryList)
                    ? approvedCategoryList
                    : new List<string> { category };
                var translated = translatedIngredients.TryGetValue(ingredient, out var translatedValue) ? translatedValue : null;
                var query = GetSearchQuery(ingredient, categories.FirstOrDefault() ?? "auto", translated);
                return new
                {
                    Ingredient = ingredient,
                    Category = category,
                    Query = query,
                    Products = SearchProductsForCategoriesAsync(query, categories, model.ForceRefresh)
                };
            }).ToList();

            await Task.WhenAll(searches.Select(search => (Task)search.Products).Append(storesTask));

            var nearbyStores = (await storesTask)
                .Select(store => new StoreWithDistance(store, CalculateDistanceKm(model.Latitude, model.Longitude, store.Lat, store.Lng)))
                .OrderBy(store => store.DistanceKm)
                .ToList();
            var unmatchedIngredients = new List<string>();
            var resultOffers = new List<GroceryIngredientOfferViewModel>();

            foreach (var search in searches)
            {
                var products = await search.Products;
                var categoryPreference = await GetCategoryPreferenceAsync(search.Ingredient);
                var hasApprovedCategory = categoryPreference.Values.Any(preference => preference > 0);
                var matchedProducts = products
                    .Select(product => new
                    {
                        Product = product,
                        Store = FindNearestStore(nearbyStores, product.Store),
                        MatchScore = GetMatchScore(search.Query, search.Category, product),
                        CategoryPreference = categoryPreference.TryGetValue(NormalizeFeedbackValue(product.Category), out var preference) ? preference : 0
                    })
                    .Where(match => match.Store != null &&
                        match.MatchScore < int.MaxValue &&
                        (shoppingPreference != "organic" || IsOrganic(match.Product)));

                var preferredProducts = matchedProducts
                    .Where(match => categoryPreference.TryGetValue(NormalizeFeedbackValue(match.Product.Category), out var preference)
                        ? hasApprovedCategory ? preference > 0 : preference >= 0
                        : !hasApprovedCategory)
                    .ToList();
                var fallbackProducts = matchedProducts
                    .Where(match => categoryPreference.TryGetValue(NormalizeFeedbackValue(match.Product.Category), out var preference) && preference < 0)
                    .ToList();

                var rankedProducts = shoppingPreference switch
                {
                    "budget" => preferredProducts
                        .OrderByDescending(match => match.CategoryPreference)
                        .ThenBy(match => match.MatchScore)
                        .ThenBy(match => match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm),
                    "deals" => preferredProducts
                        .OrderByDescending(match => match.CategoryPreference)
                        .ThenBy(match => match.MatchScore)
                        .ThenByDescending(match => GetDiscountPercentage(match.Product))
                        .ThenBy(match => match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm),
                    "premium" => preferredProducts
                        .OrderByDescending(match => match.CategoryPreference)
                        .ThenBy(match => match.MatchScore)
                        .ThenByDescending(match => match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm),
                    _ => preferredProducts
                        .OrderByDescending(match => match.CategoryPreference)
                        .ThenBy(match => match.MatchScore)
                        .ThenByDescending(match => match.Product.OldPrice > match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm)
                        .ThenBy(match => match.Product.Price)
                };

                var fallbackRankedProducts = shoppingPreference switch
                {
                    "budget" => fallbackProducts.OrderByDescending(match => match.CategoryPreference).ThenBy(match => match.MatchScore).ThenBy(match => match.Product.Price),
                    "deals" => fallbackProducts.OrderByDescending(match => match.CategoryPreference).ThenBy(match => match.MatchScore).ThenByDescending(match => GetDiscountPercentage(match.Product)).ThenBy(match => match.Product.Price),
                    "premium" => fallbackProducts.OrderByDescending(match => match.CategoryPreference).ThenBy(match => match.MatchScore).ThenByDescending(match => match.Product.Price),
                    _ => fallbackProducts.OrderByDescending(match => match.CategoryPreference).ThenBy(match => match.MatchScore).ThenBy(match => match.Store.DistanceKm).ThenBy(match => match.Product.Price)
                };
                var mappedProducts = rankedProducts
                    .Take(5)
                    .Concat(fallbackRankedProducts.Take(Math.Max(0, 5 - preferredProducts.Count)))
                    .ToList();

                if (mappedProducts.Count == 0)
                {
                    unmatchedIngredients.Add(search.Ingredient);
                    continue;
                }

                foreach (var match in mappedProducts)
                {
                    var product = match.Product;
                    var originalPrice = product.OldPrice > product.Price ? product.OldPrice : null;
                    var discountPercentage = originalPrice > 0
                        ? (int?)Math.Round((originalPrice.Value - product.Price) / originalPrice.Value * 100, MidpointRounding.AwayFromZero)
                        : null;
                    var identity = !string.IsNullOrWhiteSpace(product.Url)
                        ? product.Url
                        : $"{product.Store}|{product.Name}|{product.Price.ToString(CultureInfo.InvariantCulture)}";

                    resultOffers.Add(new GroceryIngredientOfferViewModel
                    {
                        IngredientName = search.Ingredient,
                        ProductCategory = product.Category,
                        ProductName = product.Name,
                        ProductId = identity,
                        OfferId = $"{search.Ingredient}|{identity}",
                        ProductUrl = product.Url,
                        ChainName = product.Store,
                        StoreName = match.Store.Store.Name,
                        StoreAddress = match.Store.Store.Address,
                        StoreCity = match.Store.Store.City,
                        StorePostalCode = match.Store.Store.PostalCode,
                        DistanceKm = Math.Round(match.Store.DistanceKm, 1),
                        Price = product.Price,
                        OriginalPrice = originalPrice,
                        DiscountPercentage = discountPercentage,
                        Currency = "DKK",
                        PriceKind = discountPercentage.HasValue ? "campaign" : "regular",
                        ImageUrl = product.ImageUrl
                    });
                }
            }

            var response = new GroceryOfferSearchResponse
            {
                Stores = nearbyStores.Take(20).Select(store => new GroceryNearbyStoreViewModel
                {
                    Id = store.Store.Id,
                    Name = store.Store.Name,
                    ChainName = store.Store.ChainName,
                    Address = store.Store.Address,
                    City = store.Store.City,
                    PostalCode = store.Store.PostalCode,
                    Latitude = store.Store.Lat,
                    Longitude = store.Store.Lng,
                    DistanceKm = Math.Round(store.DistanceKm, 1)
                }).ToList(),
                Offers = resultOffers,
                UnmatchedIngredients = unmatchedIngredients.OrderBy(name => name).ToList(),
                AvailableCategories = marketFilters,
                IngredientDisplayNames = translatedIngredients.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                GeneratedAtUtc = DateTime.UtcNow
            };

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));
            return response;
        }

        private async Task<List<string>> GetMarketFiltersAsync(bool forceRefresh)
        {
            const string cacheKey = "madpris:filters";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<string> cached)) return cached;

            try
            {
                var client = _httpClientFactory.CreateClient("Madpris");
                using var response = await client.GetAsync("api/filters");
                if (!response.IsSuccessStatusCode) return new List<string>();

                var filters = await response.Content.ReadFromJsonAsync<MadprisFilters>(JsonOptions);
                var categories = (filters?.Categories ?? new List<string>())
                    .Concat((filters?.Subcategories ?? new Dictionary<string, List<string>>()).Values.SelectMany(values => values ?? new List<string>()))
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _cache.Set(cacheKey, categories, TimeSpan.FromHours(12));
                return categories;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Madpris filters could not be loaded; using product categories");
                return new List<string>();
            }
        }

        private async Task<List<MadprisProduct>> SearchProductsAsync(string ingredient, string category, bool forceRefresh)
        {
            var query = NormalizeSearchTerm(ingredient);
            if (query.Length < 2)
            {
                return new List<MadprisProduct>();
            }

            var categoryFilter = GetMadprisCategoryFilter(category);
            var cacheKey = $"madpris:products:{query.ToLowerInvariant()}:{categoryFilter.ToLowerInvariant()}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<MadprisProduct> cached))
            {
                return cached;
            }

            var client = _httpClientFactory.CreateClient("Madpris");
            var categoryQuery = string.IsNullOrWhiteSpace(categoryFilter) ? string.Empty : $"&category={Uri.EscapeDataString(categoryFilter)}";
            using var response = await client.GetAsync($"api/merged-products?q={Uri.EscapeDataString(query)}&page=1{categoryQuery}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<MadprisProduct>();
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Madpris returned {StatusCode} for product search", (int)response.StatusCode);
                throw new GroceryOfferProviderException((int)response.StatusCode);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<MadprisMergedProductSearchResponse>(stream, JsonOptions);
            if ((result?.Products == null || result.Products.Count == 0) && !string.IsNullOrWhiteSpace(categoryFilter))
            {
                using var fallbackResponse = await client.GetAsync($"api/merged-products?q={Uri.EscapeDataString(query)}&page=1");
                if (fallbackResponse.IsSuccessStatusCode)
                {
                    using var fallbackStream = await fallbackResponse.Content.ReadAsStreamAsync();
                    result = await JsonSerializer.DeserializeAsync<MadprisMergedProductSearchResponse>(fallbackStream, JsonOptions);
                }
            }
            var products = (result?.Products ?? new List<MadprisMergedProduct>())
                .SelectMany(product => (product.Stores ?? new List<MadprisMergedStore>()).Select(store => new MadprisProduct
                {
                    Name = store.Name ?? product.Name,
                    Store = store.Store,
                    Brand = store.Brand ?? product.Brand,
                    Description = store.Description ?? product.Description,
                    Category = product.Category,
                    Price = store.Price,
                    OldPrice = store.OldPrice,
                    ImageUrl = store.ImageUrl ?? product.ImageUrl,
                    Url = store.Url,
                    GroupId = product.GroupId
                }))
                .ToList();
            _cache.Set(cacheKey, products, TimeSpan.FromHours(1));
            return products;
        }

        private async Task<List<MadprisProduct>> SearchProductsForCategoriesAsync(string query, IReadOnlyList<string> categories, bool forceRefresh)
        {
            var categoryList = (categories ?? new List<string> { "auto" })
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var results = await Task.WhenAll(categoryList.SelectMany(category => GetQueryVariants(query).Select(queryVariant => SearchProductsAsync(queryVariant, category, forceRefresh))));
            return results.SelectMany(products => products)
                .GroupBy(product => product.GroupId > 0
                    ? $"group:{product.GroupId}"
                    : $"{product.Store}|{product.Name}|{product.Price.ToString(CultureInfo.InvariantCulture)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IReadOnlyList<string> GetQueryVariants(string query)
        {
            var normalized = query?.Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return Array.Empty<string>();
            var plural = normalized.EndsWith("e", StringComparison.OrdinalIgnoreCase)
                ? normalized + "r"
                : normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                    ? normalized
                    : normalized + "er";
            return new[] { normalized, plural }.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string GetMadprisCategoryFilter(string category)
        {
            return category?.Trim().ToLowerInvariant() switch
            {
                null or "" or "auto" => string.Empty,
                "produce" => "Frugt & grønt",
                "dairy" => "Mejeri",
                "meat" => "Kød",
                "bakery" => "Brød & kager",
                "beverages" => "Drikkevarer",
                "candy" => "Slik & snacks",
                "chocolate" => "Slik & snacks",
                "pantry" => "Kolonial",
                _ => category.Trim()
            };
        }

        private async Task<Dictionary<string, int>> GetCategoryPreferenceAsync(string ingredient)
        {
            var normalizedIngredient = NormalizeFeedbackValue(ingredient);
            var feedback = await _context.GroceryCategoryFeedback
                .AsNoTracking()
                .Where(feedback => feedback.IngredientName == normalizedIngredient)
                .ToDictionaryAsync(feedback => feedback.Category, feedback => feedback.ApprovalCount - feedback.RejectionCount, StringComparer.OrdinalIgnoreCase);
            return feedback;
        }

        private async Task<Dictionary<string, List<string>>> GetApprovedCategoriesAsync(IEnumerable<string> ingredients)
        {
            var normalizedIngredients = ingredients.Select(NormalizeFeedbackValue).ToList();
            var feedback = await _context.GroceryCategoryFeedback
                .AsNoTracking()
                .Where(feedback => normalizedIngredients.Contains(feedback.IngredientName))
                .OrderByDescending(feedback => feedback.ApprovalCount - feedback.RejectionCount)
                .Where(feedback => feedback.ApprovalCount > feedback.RejectionCount)
                .ToListAsync();
            return feedback
                .GroupBy(item => item.IngredientName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<ShelfAtlasStore>> GetNearbyStoresAsync(GroceryOfferSearchViewModel model, string locationKey)
        {
            var cacheKey = $"shelfatlas:stores:{locationKey}";
            if (_cache.TryGetValue(cacheKey, out List<ShelfAtlasStore> cached))
            {
                return cached;
            }

            var client = _httpClientFactory.CreateClient("ShelfAtlas");
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"stores?lat={model.Latitude.ToString(CultureInfo.InvariantCulture)}&lng={model.Longitude.ToString(CultureInfo.InvariantCulture)}&radius_km={model.RadiusKm.ToString(CultureInfo.InvariantCulture)}&limit=500");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["ShelfAtlas:ApiKey"]);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ShelfAtlas returned {StatusCode} for nearby stores", (int)response.StatusCode);
                throw new GroceryOfferProviderException((int)response.StatusCode);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<ShelfAtlasEnvelope<ShelfAtlasStore>>(stream, JsonOptions);
            var stores = result?.Data ?? new List<ShelfAtlasStore>();
            _cache.Set(cacheKey, stores, TimeSpan.FromMinutes(30));
            return stores;
        }

        private static StoreWithDistance FindNearestStore(List<StoreWithDistance> stores, string chainName)
        {
            var normalizedChain = NormalizeComparisonText(chainName);
            return stores.FirstOrDefault(store =>
                NormalizeComparisonText(store.Store.ChainName) == normalizedChain ||
                NormalizeComparisonText(store.Store.ChainSlug) == normalizedChain);
        }

        private static int GetMatchScore(string query, string category, MadprisProduct product)
        {
            var normalizedQuery = NormalizeWords(query);
            var name = NormalizeWords(product.Name);
            var description = NormalizeWords(product.Description);
            var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var descriptionWords = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var categoryScore = GetCategoryMatchScore(category, product);
            if (categoryScore == int.MaxValue) return int.MaxValue;
            if (IsObviouslyUnrelatedProduct(normalizedQuery, name)) return int.MaxValue;
            if (name == normalizedQuery) return categoryScore;
            if (ContainsPhrase(name, normalizedQuery)) return categoryScore + 1;
            if (!normalizedQuery.Contains(' ') && nameWords.Any(word => word.StartsWith(normalizedQuery, StringComparison.Ordinal) ||
                word.EndsWith(normalizedQuery, StringComparison.Ordinal))) return categoryScore + 2;
            if (ContainsPhrase(description, normalizedQuery) ||
                (!normalizedQuery.Contains(' ') && descriptionWords.Contains(normalizedQuery, StringComparer.Ordinal))) return categoryScore + 3;
            return int.MaxValue;
        }

        private static int GetCategoryMatchScore(string category, MadprisProduct product)
        {
            if (category == "auto")
            {
                return IsNonGroceryCategory(product.Category) ? int.MaxValue : 10;
            }

            return MatchesCategory(category, product) ? 0 : int.MaxValue;
        }

        private static bool IsObviouslyUnrelatedProduct(string query, string productName)
        {
            string[] unrelatedTerms = { "snack", "chips", "slik", "bolcher", "kage", "dessert", "drik", "soda", "saebe", "shampoo", "hund", "kat" };
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var productWords = productName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return unrelatedTerms.Any(term => !queryWords.Contains(term, StringComparer.Ordinal) &&
                productWords.Contains(term, StringComparer.Ordinal));
        }

        private static bool ContainsPhrase(string value, string phrase) =>
            $" {value} ".Contains($" {phrase} ", StringComparison.Ordinal);

        private static decimal GetDiscountPercentage(MadprisProduct product) =>
            product.OldPrice > product.Price && product.OldPrice > 0
                ? (product.OldPrice.Value - product.Price) / product.OldPrice.Value
                : 0;

        private static bool IsOrganic(MadprisProduct product)
        {
            var productText = NormalizeComparisonText($"{product.Name} {product.Description} {product.Category}");
            var productWords = NormalizeWords($"{product.Name} {product.Description} {product.Category}")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return productWords.Contains("øko", StringComparer.Ordinal) ||
                productWords.Contains("oko", StringComparer.Ordinal) ||
                productText.Contains("økologisk", StringComparison.Ordinal) ||
                productText.Contains("okologisk", StringComparison.Ordinal) ||
                productText.Contains("organic", StringComparison.Ordinal);
        }

        private static bool MatchesCategory(string category, MadprisProduct product)
        {
            var normalizedCategory = NormalizeComparisonText(product.Category);
            var normalizedName = NormalizeComparisonText(product.Name);
            if (!string.IsNullOrWhiteSpace(category) && NormalizeComparisonText(category) == normalizedCategory)
            {
                return true;
            }

            return category switch
            {
                "bakery" => normalizedCategory.Contains("bager", StringComparison.Ordinal) || normalizedCategory.Contains("brod", StringComparison.Ordinal),
                "beverages" => normalizedCategory.Contains("drikke", StringComparison.Ordinal),
                "candy" => normalizedCategory.Contains("slik", StringComparison.Ordinal) || normalizedCategory.Contains("kiosk", StringComparison.Ordinal),
                "chocolate" => normalizedName.Contains("chokolade", StringComparison.Ordinal),
                "dairy" => normalizedCategory.Contains("mejeri", StringComparison.Ordinal) || normalizedCategory.Contains("kol", StringComparison.Ordinal),
                "meat" => normalizedCategory.Contains("kod", StringComparison.Ordinal),
                "pantry" => normalizedCategory.Contains("kolonial", StringComparison.Ordinal) || normalizedCategory.Contains("krydder", StringComparison.Ordinal) || normalizedCategory.Contains("fodevarer", StringComparison.Ordinal),
                "produce" => normalizedCategory.Contains("frugt", StringComparison.Ordinal) || normalizedCategory.Contains("gront", StringComparison.Ordinal),
                _ => !IsNonGroceryCategory(product.Category)
            };
        }

        private static bool IsNonGroceryCategory(string category)
        {
            var normalizedCategory = NormalizeComparisonText(category);
            return normalizedCategory.Contains("slik", StringComparison.Ordinal) ||
                normalizedCategory.Contains("kiosk", StringComparison.Ordinal) ||
                normalizedCategory.Contains("personligpleje", StringComparison.Ordinal) ||
                normalizedCategory.Contains("husholdning", StringComparison.Ordinal) ||
                normalizedCategory.Contains("bolig", StringComparison.Ordinal) ||
                normalizedCategory.Contains("skonhed", StringComparison.Ordinal);
        }

        private static string GetDanishSearchQuery(string ingredient)
        {
            return NormalizeSearchTerm(ingredient);
        }

        private static string GetSearchQuery(string ingredient, string category, string translatedIngredient = null)
        {
            var broadQuery = GetBroadDanishSearchQuery(ingredient, translatedIngredient);
            return category switch
            {
                "chocolate" when broadQuery == "æg" => "chokoladeæg",
                "chocolate" => $"{broadQuery} chokolade",
                "candy" => broadQuery,
                _ => translatedIngredient ?? GetDanishSearchQuery(ingredient)
            };
        }

        private static string GetBroadDanishSearchQuery(string ingredient, string translatedIngredient = null)
        {
            return NormalizeSearchTerm(ingredient).ToLowerInvariant() switch
            {
                "egg" or "eggs" => "æg",
                "milk" => "mælk",
                _ => translatedIngredient ?? GetDanishSearchQuery(ingredient)
            };
        }

        private static string GetCategory(GroceryOfferSearchViewModel model, string ingredient)
        {
            if (model.IngredientCategories != null &&
                model.IngredientCategories.TryGetValue(ingredient, out var category) &&
                !string.IsNullOrWhiteSpace(category))
            {
                return category.Trim().ToLowerInvariant();
            }

            return "auto";
        }

        private static string GetShoppingPreference(GroceryOfferSearchViewModel model)
        {
            var preference = model.ShoppingPreference?.Trim().ToLowerInvariant();
            return preference is "budget" or "deals" or "organic" or "premium" ? preference : "balanced";
        }

        private static string NormalizeSearchTerm(string ingredient)
        {
            var parenthesisIndex = ingredient.IndexOf('(');
            var value = parenthesisIndex >= 0 ? ingredient.Substring(0, parenthesisIndex) : ingredient;
            return value.Trim();
        }

        private static string NormalizeFeedbackValue(string value) => string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static string NormalizeComparisonText(string value)
        {
            return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                .ToArray());
        }

        private static string NormalizeWords(string value)
        {
            var normalized = (value ?? string.Empty)
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);
            return string.Join(' ', normalized
                .Select(character => CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark
                    ? '\0'
                    : char.IsLetterOrDigit(character) ? character : ' ')
                .Where(character => character != '\0')
                .Aggregate(new List<string> { string.Empty }, (words, character) =>
                {
                    if (character == ' ')
                    {
                        if (words[words.Count - 1].Length > 0) words.Add(string.Empty);
                    }
                    else
                    {
                        words[words.Count - 1] += character;
                    }
                    return words;
                })
                .Where(word => word.Length > 0));
        }

        private static double CalculateDistanceKm(double latitude, double longitude, double targetLatitude, double targetLongitude)
        {
            const double earthRadiusKm = 6371;
            var latitudeDelta = DegreesToRadians(targetLatitude - latitude);
            var longitudeDelta = DegreesToRadians(targetLongitude - longitude);
            var firstLatitude = DegreesToRadians(latitude);
            var secondLatitude = DegreesToRadians(targetLatitude);
            var calculation = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
                Math.Cos(firstLatitude) * Math.Cos(secondLatitude) * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
            return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(calculation), Math.Sqrt(1 - calculation));
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

        private class MadprisProductSearchResponse
        {
            public List<MadprisProduct> Products { get; set; } = new List<MadprisProduct>();
        }

        private class MadprisMergedProductSearchResponse
        {
            public List<MadprisMergedProduct> Products { get; set; } = new List<MadprisMergedProduct>();
        }

        private class MadprisMergedProduct
        {
            [JsonPropertyName("group_id")]
            public int GroupId { get; set; }
            public string Name { get; set; }
            public string Brand { get; set; }
            [JsonPropertyName("desc")]
            public string Description { get; set; }
            public string Category { get; set; }
            [JsonPropertyName("img")]
            public string ImageUrl { get; set; }
            public List<MadprisMergedStore> Stores { get; set; } = new List<MadprisMergedStore>();
        }

        private class MadprisMergedStore
        {
            public string Store { get; set; }
            public string Name { get; set; }
            public string Brand { get; set; }
            [JsonPropertyName("desc")]
            public string Description { get; set; }
            public decimal Price { get; set; }
            [JsonPropertyName("old_price")]
            public decimal? OldPrice { get; set; }
            [JsonPropertyName("img")]
            public string ImageUrl { get; set; }
            public string Url { get; set; }
        }

        private class MadprisFilters
        {
            public List<string> Categories { get; set; } = new List<string>();
            public Dictionary<string, List<string>> Subcategories { get; set; } = new Dictionary<string, List<string>>();
        }

        private class MadprisProduct
        {
            public string Name { get; set; }
            public string Store { get; set; }
            public string Brand { get; set; }
            [JsonPropertyName("desc")]
            public string Description { get; set; }
            public string Category { get; set; }
            public decimal Price { get; set; }
            [JsonPropertyName("old_price")]
            public decimal? OldPrice { get; set; }
            [JsonPropertyName("img")]
            public string ImageUrl { get; set; }
            public string Url { get; set; }
            public int GroupId { get; set; }
        }

        private class ShelfAtlasEnvelope<T>
        {
            public List<T> Data { get; set; }
        }

        private class ShelfAtlasStore
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string ChainSlug { get; set; }
            public string ChainName { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string PostalCode { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
        }

        private class StoreWithDistance
        {
            public StoreWithDistance(ShelfAtlasStore store, double distanceKm)
            {
                Store = store;
                DistanceKm = distanceKm;
            }

            public ShelfAtlasStore Store { get; }
            public double DistanceKm { get; }
        }
    }

    public class GroceryOfferProviderException : Exception
    {
        public GroceryOfferProviderException(int statusCode)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}