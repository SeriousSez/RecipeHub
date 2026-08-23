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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public interface IGroceryOfferService
    {
        bool IsLocationConfigured { get; }
        Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model);
    }

    public class MadprisGroceryOfferService : IGroceryOfferService
    {
        private static readonly IReadOnlyDictionary<string, string> DanishIngredientQueries =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apple"] = "æble",
                ["apples"] = "æbler",
                ["basil"] = "basilikum",
                ["beef"] = "oksekød",
                ["black pepper"] = "sort peber",
                ["butter"] = "smør",
                ["carrot"] = "gulerod",
                ["carrots"] = "gulerødder",
                ["cheese"] = "ost",
                ["chicken"] = "kylling",
                ["chicken breast"] = "kyllingebryst",
                ["chives"] = "purløg",
                ["cream"] = "fløde",
                ["cucumber"] = "agurk",
                ["egg"] = "frilandsæg",
                ["eggs"] = "frilandsæg",
                ["flour"] = "mel",
                ["fresh chives, finely chopped"] = "afskåret purløg",
                ["garlic"] = "hvidløg",
                ["lemon"] = "citron",
                ["lemons"] = "citroner",
                ["milk"] = "letmælk",
                ["mushroom"] = "champignon",
                ["mushrooms"] = "champignon",
                ["olive oil"] = "olivenolie",
                ["onion"] = "løg",
                ["onions"] = "løg",
                ["pepper"] = "peber",
                ["potato"] = "kartoffel",
                ["potatoes"] = "kartofler",
                ["rice"] = "ris",
                ["salt"] = "bordsalt",
                ["spinach"] = "spinat",
                ["sugar"] = "sukker",
                ["sumac"] = "sumak",
                ["tomato"] = "tomat",
                ["tomatoes"] = "tomater",
                ["yogurt"] = "yoghurt"
            };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MadprisGroceryOfferService> _logger;

        public MadprisGroceryOfferService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<MadprisGroceryOfferService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public bool IsLocationConfigured => !string.IsNullOrWhiteSpace(_configuration["ShelfAtlas:ApiKey"]);

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            var locationKey = $"{Math.Round(model.Latitude, 3)}:{Math.Round(model.Longitude, 3)}:{Math.Round(model.RadiusKm, 1)}";
            var categoryKey = string.Join("|", ingredients.Select(ingredient => $"{ingredient}:{GetCategory(model, ingredient)}"));
            var shoppingPreference = GetShoppingPreference(model);
            var cacheKey = $"groceryoffers:{locationKey}:{shoppingPreference}:{categoryKey.ToLowerInvariant()}";
            if (!model.ForceRefresh && _cache.TryGetValue(cacheKey, out GroceryOfferSearchResponse cached))
            {
                return cached;
            }

            var storesTask = GetNearbyStoresAsync(model, locationKey);
            var searches = ingredients.Select(ingredient =>
            {
                var category = GetCategory(model, ingredient);
                var query = GetSearchQuery(ingredient, category);
                return new
                {
                    Ingredient = ingredient,
                    Category = category,
                    Query = query,
                    Products = SearchProductsAsync(query, model.ForceRefresh)
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
                var matchedProducts = products
                    .Select(product => new
                    {
                        Product = product,
                        Store = FindNearestStore(nearbyStores, product.Store),
                        MatchScore = GetMatchScore(search.Query, search.Category, product)
                    })
                    .Where(match => match.Store != null &&
                        match.MatchScore < int.MaxValue &&
                        (shoppingPreference != "organic" || IsOrganic(match.Product)));

                var rankedProducts = shoppingPreference switch
                {
                    "budget" => matchedProducts
                        .OrderBy(match => match.MatchScore)
                        .ThenBy(match => match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm),
                    "deals" => matchedProducts
                        .OrderBy(match => match.MatchScore)
                        .ThenByDescending(match => GetDiscountPercentage(match.Product))
                        .ThenBy(match => match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm),
                    "premium" => matchedProducts
                        .OrderBy(match => match.MatchScore)
                        .ThenByDescending(match => match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm),
                    _ => matchedProducts
                        .OrderBy(match => match.MatchScore)
                        .ThenByDescending(match => match.Product.OldPrice > match.Product.Price)
                        .ThenBy(match => match.Store.DistanceKm)
                        .ThenBy(match => match.Product.Price)
                };

                var mappedProducts = rankedProducts
                    .Take(5)
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
                GeneratedAtUtc = DateTime.UtcNow
            };

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));
            return response;
        }

        private async Task<List<MadprisProduct>> SearchProductsAsync(string ingredient, bool forceRefresh)
        {
            var query = NormalizeSearchTerm(ingredient);
            if (query.Length < 2)
            {
                return new List<MadprisProduct>();
            }

            var cacheKey = $"madpris:products:{query.ToLowerInvariant()}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<MadprisProduct> cached))
            {
                return cached;
            }

            var client = _httpClientFactory.CreateClient("Madpris");
            using var response = await client.GetAsync($"api/products?q={Uri.EscapeDataString(query)}&page=1");
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
            var result = await JsonSerializer.DeserializeAsync<MadprisProductSearchResponse>(stream, JsonOptions);
            var products = result?.Products ?? new List<MadprisProduct>();
            _cache.Set(cacheKey, products, TimeSpan.FromHours(1));
            return products;
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

            if (!MatchesCategory(category, product)) return int.MaxValue;
            if (name == normalizedQuery) return 0;
            if (ContainsPhrase(name, normalizedQuery)) return 1;
            if (!normalizedQuery.Contains(' ') && nameWords.Any(word => word.StartsWith(normalizedQuery, StringComparison.Ordinal) ||
                word.EndsWith(normalizedQuery, StringComparison.Ordinal))) return 2;
            if (ContainsPhrase(description, normalizedQuery) ||
                (!normalizedQuery.Contains(' ') && descriptionWords.Contains(normalizedQuery, StringComparer.Ordinal))) return 3;
            return int.MaxValue;
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
            var searchTerm = NormalizeSearchTerm(ingredient);
            return DanishIngredientQueries.TryGetValue(searchTerm, out var translated) ? translated : searchTerm;
        }

        private static string GetSearchQuery(string ingredient, string category)
        {
            var broadQuery = GetBroadDanishSearchQuery(ingredient);
            return category switch
            {
                "chocolate" when broadQuery == "æg" => "chokoladeæg",
                "chocolate" => $"{broadQuery} chokolade",
                "candy" => broadQuery,
                _ => GetDanishSearchQuery(ingredient)
            };
        }

        private static string GetBroadDanishSearchQuery(string ingredient)
        {
            return NormalizeSearchTerm(ingredient).ToLowerInvariant() switch
            {
                "egg" or "eggs" => "æg",
                "milk" => "mælk",
                _ => GetDanishSearchQuery(ingredient)
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