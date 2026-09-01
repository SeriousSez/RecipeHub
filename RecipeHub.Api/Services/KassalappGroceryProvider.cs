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
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    // Kassalapp's exact product-search response shape wasn't fully confirmable from public docs at
    // implementation time, so parsing here is deliberately defensive (tries several likely field names
    // per value) rather than a strict DTO. If results come back empty, check the raw response and adjust
    // the GetString/GetDecimal calls in ParseProduct below.
    public class KassalappGroceryProvider : IGroceryProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<KassalappGroceryProvider> _logger;
        private readonly IRecipeTranslationService _translationService;

        public KassalappGroceryProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache, ILogger<KassalappGroceryProvider> logger, IRecipeTranslationService translationService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _translationService = translationService;
        }

        public string CountryCode => "NO";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["Kassalapp:ApiKey"]);

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var translatedIngredients = await _translationService.TranslateIngredientNamesAsync(ingredients, "Norwegian", model.IngredientContexts)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var distanceByChain = await ResolveChainDistancesAsync(model.Latitude, model.Longitude, Math.Max(model.RadiusKm, 10), model.ForceRefresh);

            var searches = ingredients.Select(async ingredient => new
            {
                Ingredient = ingredient,
                Products = await SearchAsync(translatedIngredients.TryGetValue(ingredient, out var translated) ? translated : ingredient, model.ForceRefresh)
            }).ToList();
            var results = await Task.WhenAll(searches);

            var offers = (await Task.WhenAll(results.SelectMany(result => result.Products.Take(5).Select(product => MapOfferAsync(result.Ingredient, product, distanceByChain)))))
                .Where(offer => offer != null)
                .ToList();
            var stores = offers.GroupBy(offer => offer.ChainName, StringComparer.OrdinalIgnoreCase).Select(group => new GroceryNearbyStoreViewModel
            {
                Id = group.Key,
                Name = group.Key,
                ChainName = group.Key,
                DistanceKm = group.Min(offer => offer.DistanceKm),
                IsOnlineOnly = group.All(offer => offer.IsOnlineOnly)
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

        private async Task<Dictionary<string, double>> ResolveChainDistancesAsync(double latitude, double longitude, double radiusKm, bool forceRefresh)
        {
            var cacheKey = $"kassalapp:stores:{Math.Round(latitude, 2)}:{Math.Round(longitude, 2)}:{Math.Round(radiusKm)}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out Dictionary<string, double> cached)) return cached;

            var apiKey = _configuration["Kassalapp:ApiKey"];
            var distanceByChain = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(apiKey) || (latitude == 0 && longitude == 0)) return distanceByChain;

            try
            {
                var client = _httpClientFactory.CreateClient("Kassalapp");
                using var message = new HttpRequestMessage(HttpMethod.Get,
                    $"physical-stores?lat={latitude.ToString(CultureInfo.InvariantCulture)}&lng={longitude.ToString(CultureInfo.InvariantCulture)}&km={radiusKm.ToString(CultureInfo.InvariantCulture)}&size=100");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Kassalapp physical-stores lookup failed with status {StatusCode}: {ErrorBody}", (int)response.StatusCode, errorBody);
                    return distanceByChain;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var storeElement in dataElement.EnumerateArray())
                    {
                        var group = GetString(storeElement, "group");
                        var chainKey = NormalizeChainKey(group);
                        if (chainKey == null || !storeElement.TryGetProperty("position", out var position)) continue;

                        var storeLat = GetDouble(position, "lat");
                        var storeLng = GetDouble(position, "lng");
                        if (storeLat == null || storeLng == null) continue;

                        var distance = HaversineDistanceKm(latitude, longitude, storeLat.Value, storeLng.Value);
                        if (!distanceByChain.TryGetValue(chainKey, out var existing) || distance < existing)
                        {
                            distanceByChain[chainKey] = distance;
                        }
                    }
                }

                _cache.Set(cacheKey, distanceByChain, TimeSpan.FromHours(6));
                return distanceByChain;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Kassalapp physical-stores lookup failed");
                return distanceByChain;
            }
        }

        private static string NormalizeChainKey(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Split('_', ' ')[0].Trim().ToLowerInvariant();

        private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371.0;
            double ToRadians(double degrees) => degrees * Math.PI / 180.0;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private async Task<List<KassalappProduct>> SearchAsync(string query, bool forceRefresh)
        {
            var cacheKey = $"kassalapp:{query.ToLowerInvariant()}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<KassalappProduct> cached)) return cached;

            var apiKey = _configuration["Kassalapp:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return new List<KassalappProduct>();

            try
            {
                var client = _httpClientFactory.CreateClient("Kassalapp");
                using var message = new HttpRequestMessage(HttpMethod.Get, $"products?search={Uri.EscapeDataString(query)}&size=10&unique=1");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Kassalapp product search failed with status {StatusCode} for query {Query}: {ErrorBody}", (int)response.StatusCode, query, errorBody);
                    return new List<KassalappProduct>();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseBody);
                var products = document.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array
                    ? dataElement.EnumerateArray().Select(ParseProduct).Where(product => product != null && !string.IsNullOrWhiteSpace(product.Name) && product.Price > 0).ToList()
                    : new List<KassalappProduct>();

                if (products.Count == 0 && dataElement.ValueKind == JsonValueKind.Array && dataElement.GetArrayLength() > 0)
                {
                    _logger.LogWarning("Kassalapp returned {Count} raw results for {Query} but none could be parsed - the response shape may differ from what's expected", dataElement.GetArrayLength(), query);
                }

                _cache.Set(cacheKey, products, TimeSpan.FromHours(1));
                return products;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Kassalapp product search failed for query {Query}", query);
                return new List<KassalappProduct>();
            }
        }

        private static KassalappProduct ParseProduct(JsonElement element)
        {
            var storeElement = element.TryGetProperty("store", out var store) && store.ValueKind == JsonValueKind.Object ? store : (JsonElement?)null;
            return new KassalappProduct
            {
                Id = GetString(element, "id") ?? GetString(element, "ean"),
                Name = GetString(element, "name"),
                Ean = GetString(element, "ean"),
                Price = GetDecimal(element, "current_price") ?? GetDecimal(element, "price") ?? 0,
                Url = GetString(element, "url") ?? GetString(element, "product_url"),
                ImageUrl = GetString(element, "image") ?? GetString(element, "image_url"),
                ChainName = storeElement != null ? GetString(storeElement.Value, "name") ?? GetString(storeElement.Value, "code") : GetString(element, "vendor")
            };
        }

        private async Task<GroceryIngredientOfferViewModel> MapOfferAsync(string ingredient, KassalappProduct product, IReadOnlyDictionary<string, double> distanceByChain)
        {
            var chainName = product.ChainName ?? "Kassalapp";
            var chainKey = NormalizeChainKey(chainName);
            var isOnlineOnly = false;
            double distanceKm = 0;

            if (chainKey != null && distanceByChain.TryGetValue(chainKey, out var resolvedDistance))
            {
                distanceKm = resolvedDistance;
            }
            else if (await IsOnlineOnlyChainAsync(chainName))
            {
                // No physical store exists anywhere for this chain (e.g. FUDI) - show it, but labeled as an online store rather than a fake "0 km".
                isOnlineOnly = true;
            }
            else
            {
                // Has physical stores somewhere, just none matched nearby - exclude rather than show a misleading distance.
                return null;
            }

            return new GroceryIngredientOfferViewModel
            {
                IngredientName = ingredient,
                ProductName = product.Name,
                ProductId = product.Id,
                OfferId = $"{ingredient}|{product.Ean}|{product.Id}",
                ProductUrl = product.Url,
                ChainName = chainName,
                StoreName = chainName,
                DistanceKm = distanceKm,
                IsOnlineOnly = isOnlineOnly,
                Price = product.Price,
                Currency = "NOK",
                PriceKind = "regular",
                ImageUrl = product.ImageUrl
            };
        }

        private async Task<bool> IsOnlineOnlyChainAsync(string chainName)
        {
            var cacheKey = $"kassalapp:onlineonly:{chainName.ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

            var apiKey = _configuration["Kassalapp:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return false;

            try
            {
                var client = _httpClientFactory.CreateClient("Kassalapp");
                using var message = new HttpRequestMessage(HttpMethod.Get, $"physical-stores?search={Uri.EscapeDataString(chainName)}&size=1");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode) return false;

                var responseBody = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseBody);
                var hasAnyPhysicalStore = document.RootElement.TryGetProperty("data", out var dataElement)
                    && dataElement.ValueKind == JsonValueKind.Array && dataElement.GetArrayLength() > 0;

                var isOnlineOnly = !hasAnyPhysicalStore;
                _cache.Set(cacheKey, isOnlineOnly, TimeSpan.FromHours(24));
                return isOnlineOnly;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Kassalapp nationwide store lookup failed for chain {ChainName}", chainName);
                return false;
            }
        }

        private static string GetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static decimal? GetDecimal(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

        private static double? GetDouble(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value)) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return null;
        }

        private class KassalappProduct
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Ean { get; set; }
            public decimal Price { get; set; }
            public string Url { get; set; }
            public string ImageUrl { get; set; }
            public string ChainName { get; set; }
        }
    }
}
