using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class PrimatGroceryProvider : IGroceryProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PrimatGroceryProvider> _logger;
        private readonly IRecipeTranslationService _translationService;

        public PrimatGroceryProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache, ILogger<PrimatGroceryProvider> logger, IRecipeTranslationService translationService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _translationService = translationService;
        }

        public string CountryCode => "SE";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["Primat:ApiKey"]);

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var translatedIngredients = await _translationService.TranslateIngredientNamesAsync(ingredients, "Swedish", model.IngredientContexts)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var resolved = await ResolveStoresAsync(model.Latitude, model.Longitude, model.ForceRefresh);
            var storesParam = resolved.DefaultSelection.Count > 0 ? string.Join(",", resolved.DefaultSelection) : null;
            var distanceByStoreKey = resolved.Stores.ToDictionary(store => $"{store.Chain}:{store.StoreId}", store => store.DistanceKm, StringComparer.OrdinalIgnoreCase);

            var searches = ingredients.Select(async ingredient => new
            {
                Ingredient = ingredient,
                Products = await SearchAsync(translatedIngredients.TryGetValue(ingredient, out var translated) ? translated : ingredient, storesParam, model.ForceRefresh)
            }).ToList();
            var results = await Task.WhenAll(searches);

            var offers = results.SelectMany(result => result.Products.Take(5).Select(product => MapOffer(result.Ingredient, product, distanceByStoreKey))).ToList();
            var stores = offers.GroupBy(offer => offer.ChainName, StringComparer.OrdinalIgnoreCase).Select(group => new GroceryNearbyStoreViewModel
            {
                Id = group.Key,
                Name = group.Key,
                ChainName = group.Key,
                DistanceKm = group.Min(offer => offer.DistanceKm)
            }).ToList();

            return new GroceryOfferSearchResponse
            {
                Stores = stores,
                Offers = offers,
                IngredientDisplayNames = translatedIngredients.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                UnmatchedIngredients = ingredients.Where(ingredient => !offers.Any(offer => offer.IngredientName.Equals(ingredient, StringComparison.OrdinalIgnoreCase))).ToList(),
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private async Task<PrimatNearbyStores> ResolveStoresAsync(double latitude, double longitude, bool forceRefresh)
        {
            var cacheKey = $"primat:stores:{Math.Round(latitude, 2)}:{Math.Round(longitude, 2)}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out PrimatNearbyStores cached)) return cached;

            var apiKey = _configuration["Primat:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || (latitude == 0 && longitude == 0)) return new PrimatNearbyStores();

            try
            {
                var client = _httpClientFactory.CreateClient("Primat");
                using var message = new HttpRequestMessage(HttpMethod.Get, $"stores/resolve?lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Primat store resolve failed with status {StatusCode}", (int)response.StatusCode);
                    return new PrimatNearbyStores();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PrimatResolveResponse>(responseBody, JsonOptions);
                var nearbyStores = new PrimatNearbyStores
                {
                    Stores = result?.Stores ?? new List<PrimatResolvedStore>(),
                    DefaultSelection = result?.DefaultSelection ?? new List<string>()
                };
                _cache.Set(cacheKey, nearbyStores, TimeSpan.FromHours(6));
                return nearbyStores;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Primat store resolve failed");
                return new PrimatNearbyStores();
            }
        }

        private async Task<List<PrimatProduct>> SearchAsync(string query, string storesParam, bool forceRefresh)
        {
            var cacheKey = $"primat:{query.ToLowerInvariant()}:{storesParam}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<PrimatProduct> cached)) return cached;

            var apiKey = _configuration["Primat:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return new List<PrimatProduct>();

            try
            {
                var client = _httpClientFactory.CreateClient("Primat");
                var storesQuery = string.IsNullOrWhiteSpace(storesParam) ? "" : $"&stores={Uri.EscapeDataString(storesParam)}";
                using var message = new HttpRequestMessage(HttpMethod.Get, $"products?q={Uri.EscapeDataString(query)}&limit=10{storesQuery}");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Primat product search failed with status {StatusCode} for query {Query}", (int)response.StatusCode, query);
                    return new List<PrimatProduct>();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PrimatSearchResponse>(responseBody, JsonOptions);
                var products = result?.Data ?? new List<PrimatProduct>();
                _cache.Set(cacheKey, products, TimeSpan.FromHours(1));
                return products;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Primat product search failed for query {Query}", query);
                return new List<PrimatProduct>();
            }
        }

        private static GroceryIngredientOfferViewModel MapOffer(string ingredient, PrimatProduct product, IReadOnlyDictionary<string, double> distanceByStoreKey)
        {
            var price = product.Prices?.Effective ?? product.Prices?.Regular ?? 0;
            var originalPrice = product.Prices?.Regular > price ? product.Prices?.Regular : (decimal?)null;
            var chainName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(product.Chain ?? "");
            var storeKey = $"{product.Chain}:{product.StoreId}";
            var distanceKm = distanceByStoreKey.TryGetValue(storeKey, out var distance) ? distance : 0;

            return new GroceryIngredientOfferViewModel
            {
                IngredientName = ingredient,
                ProductName = product.Name,
                ProductId = product.ProductId,
                OfferId = $"{ingredient}|{product.Chain}|{product.StoreId}|{product.ProductId}",
                ProductUrl = product.Urls?.Source ?? product.Urls?.Primat,
                ChainName = chainName,
                StoreName = chainName,
                DistanceKm = distanceKm,
                Price = price,
                OriginalPrice = originalPrice,
                DiscountPercentage = originalPrice > 0 ? (int?)Math.Round((originalPrice.Value - price) / originalPrice.Value * 100) : null,
                Currency = "SEK",
                PriceKind = product.Prices?.Offer != null ? "campaign" : "regular",
                ValidFrom = product.Prices?.Offer?.ValidFrom,
                ValidTo = product.Prices?.Offer?.ValidUntil,
                ImageUrl = product.Urls?.Image
            };
        }

        private class PrimatNearbyStores
        {
            public List<PrimatResolvedStore> Stores { get; set; } = new List<PrimatResolvedStore>();
            public List<string> DefaultSelection { get; set; } = new List<string>();
        }

        private class PrimatResolveResponse
        {
            [JsonPropertyName("stores")] public List<PrimatResolvedStore> Stores { get; set; } = new List<PrimatResolvedStore>();
            [JsonPropertyName("default_selection")] public List<string> DefaultSelection { get; set; } = new List<string>();
        }

        private class PrimatResolvedStore
        {
            [JsonPropertyName("chain")] public string Chain { get; set; }
            [JsonPropertyName("store_id")] public string StoreId { get; set; }
            [JsonPropertyName("km")] public double DistanceKm { get; set; }
        }

        private class PrimatSearchResponse
        {
            [JsonPropertyName("data")] public List<PrimatProduct> Data { get; set; } = new List<PrimatProduct>();
        }

        private class PrimatProduct
        {
            [JsonPropertyName("chain")] public string Chain { get; set; }
            [JsonPropertyName("store_id")] public string StoreId { get; set; }
            [JsonPropertyName("product_id")] public string ProductId { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("gtin")] public string Gtin { get; set; }
            [JsonPropertyName("prices")] public PrimatPrices Prices { get; set; }
            [JsonPropertyName("urls")] public PrimatUrls Urls { get; set; }
        }

        private class PrimatPrices
        {
            [JsonPropertyName("regular")] public decimal? Regular { get; set; }
            [JsonPropertyName("member")] public decimal? Member { get; set; }
            [JsonPropertyName("effective")] public decimal? Effective { get; set; }
            [JsonPropertyName("offer")] public PrimatOffer Offer { get; set; }
        }

        private class PrimatOffer
        {
            [JsonPropertyName("price")] public decimal? Price { get; set; }
            [JsonPropertyName("label")] public string Label { get; set; }
            [JsonPropertyName("valid_from")] public DateTime? ValidFrom { get; set; }
            [JsonPropertyName("valid_until")] public DateTime? ValidUntil { get; set; }
        }

        private class PrimatUrls
        {
            [JsonPropertyName("primat")] public string Primat { get; set; }
            [JsonPropertyName("source")] public string Source { get; set; }
            [JsonPropertyName("image")] public string Image { get; set; }
        }
    }
}
