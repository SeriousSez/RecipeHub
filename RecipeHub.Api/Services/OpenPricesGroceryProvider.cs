using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class OpenPricesGroceryProvider : IGroceryProvider
    {
        private static readonly IReadOnlyDictionary<string, string> EstonianIngredientQueries =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apple"] = "õun",
                ["apples"] = "õun",
                ["beef"] = "veiseliha",
                ["butter"] = "või",
                ["carrot"] = "porgand",
                ["carrots"] = "porgand",
                ["cheese"] = "juust",
                ["chicken"] = "kana",
                ["cream"] = "koor",
                ["cucumber"] = "kurk",
                ["egg"] = "muna",
                ["eggs"] = "muna",
                ["flour"] = "jahu",
                ["garlic"] = "küüslauk",
                ["lemon"] = "sidrun",
                ["milk"] = "piim",
                ["mushroom"] = "seen",
                ["olive oil"] = "oliiviõli",
                ["onion"] = "sibul",
                ["potato"] = "kartul",
                ["rice"] = "riis",
                ["salt"] = "sool",
                ["spinach"] = "spinat",
                ["sugar"] = "suhkur",
                ["tomato"] = "tomat",
                ["tomatoes"] = "tomat",
                ["yogurt"] = "jogurt"
            };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OpenPricesGroceryProvider> _logger;

        public OpenPricesGroceryProvider(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<OpenPricesGroceryProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
        }

        public string CountryCode => "EE";
        public bool IsConfigured => true;

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var searches = ingredients.Select(async ingredient => new { Ingredient = ingredient, Prices = await SearchAsync(GetQuery(ingredient), model) }).ToList();
            var results = await Task.WhenAll(searches);
            var offers = results.SelectMany(result => result.Prices.Take(5).Select(price => MapOffer(result.Ingredient, price, model))).ToList();
            var stores = offers.GroupBy(offer => offer.StoreName, StringComparer.OrdinalIgnoreCase).Select(group =>
            {
                var price = results.SelectMany(result => result.Prices).First(value => GetStoreName(value.Location).Equals(group.Key, StringComparison.OrdinalIgnoreCase));
                return new GroceryNearbyStoreViewModel
                {
                    Id = price.Location.Id.ToString(CultureInfo.InvariantCulture),
                    Name = group.Key,
                    ChainName = price.Location.OsmBrand ?? group.Key,
                    Address = price.Location.OsmDisplayName,
                    City = price.Location.OsmAddressCity,
                    PostalCode = price.Location.OsmAddressPostcode,
                    Latitude = price.Location.OsmLat ?? model.Latitude,
                    Longitude = price.Location.OsmLon ?? model.Longitude,
                    DistanceKm = group.Min(offer => offer.DistanceKm)
                };
            }).ToList();

            return new GroceryOfferSearchResponse
            {
                Stores = stores,
                Offers = offers,
                UnmatchedIngredients = ingredients.Where(ingredient => !offers.Any(offer => offer.IngredientName.Equals(ingredient, StringComparison.OrdinalIgnoreCase))).ToList(),
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private async Task<List<OpenPrice>> SearchAsync(string query, GroceryOfferSearchViewModel model)
        {
            var cacheKey = $"openprices:{query.ToLowerInvariant()}:{Math.Round(model.Latitude, 2)}:{Math.Round(model.Longitude, 2)}:{Math.Round(model.RadiusKm)}";
            if (!model.ForceRefresh && _cache.TryGetValue(cacheKey, out List<OpenPrice> cached)) return cached;

            var minimumDate = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var url = $"api/v1/prices?product_name={Uri.EscapeDataString(query)}&lat={model.Latitude.ToString(CultureInfo.InvariantCulture)}&lon={model.Longitude.ToString(CultureInfo.InvariantCulture)}&radius_km={model.RadiusKm.ToString(CultureInfo.InvariantCulture)}&date__gte={minimumDate}&order_by=-date&size=20";
            var client = _httpClientFactory.CreateClient("OpenPrices");
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open Prices returned {StatusCode}", (int)response.StatusCode);
                throw new GroceryOfferProviderException((int)response.StatusCode);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<OpenPriceResponse>(stream, JsonOptions);
            var prices = (result?.Items ?? new List<OpenPrice>()).Where(price => price.Price > 0 && price.Location != null && string.Equals(price.Location.OsmAddressCountryCode, "EE", StringComparison.OrdinalIgnoreCase)).ToList();
            _cache.Set(cacheKey, prices, TimeSpan.FromHours(1));
            return prices;
        }

        private static GroceryIngredientOfferViewModel MapOffer(string ingredient, OpenPrice price, GroceryOfferSearchViewModel model)
        {
            var productName = price.Product?.ProductName ?? price.ProductName ?? ingredient;
            var storeName = GetStoreName(price.Location);
            var distance = price.Location.OsmLat.HasValue && price.Location.OsmLon.HasValue
                ? CalculateDistanceKm(model.Latitude, model.Longitude, price.Location.OsmLat.Value, price.Location.OsmLon.Value)
                : 0;
            return new GroceryIngredientOfferViewModel
            {
                IngredientName = ingredient,
                ProductName = productName,
                ProductId = price.ProductCode ?? $"openprices-{price.ProductId}",
                OfferId = $"{ingredient}|openprices|{price.Id}",
                ChainName = price.Location.OsmBrand ?? storeName,
                StoreName = storeName,
                StoreAddress = price.Location.OsmDisplayName,
                StoreCity = price.Location.OsmAddressCity,
                StorePostalCode = price.Location.OsmAddressPostcode,
                DistanceKm = Math.Round(distance, 1),
                Price = price.Price.Value,
                OriginalPrice = price.PriceIsDiscounted ? price.PriceWithoutDiscount : null,
                Currency = price.Currency ?? "EUR",
                PriceKind = price.PriceIsDiscounted ? "campaign" : "regular",
                ValidFrom = price.Date,
                ImageUrl = price.Product?.ImageUrl
            };
        }

        private static string GetStoreName(OpenPriceLocation location) => location.OsmName ?? location.OsmBrand ?? "Open Prices";

        private static string GetQuery(string ingredient)
        {
            var parenthesisIndex = ingredient.IndexOf('(');
            var normalized = (parenthesisIndex >= 0 ? ingredient.Substring(0, parenthesisIndex) : ingredient).Trim();
            return EstonianIngredientQueries.TryGetValue(normalized, out var translated) ? translated : normalized;
        }

        private static double CalculateDistanceKm(double latitude, double longitude, double targetLatitude, double targetLongitude)
        {
            const double earthRadiusKm = 6371;
            var latitudeDelta = (targetLatitude - latitude) * Math.PI / 180;
            var longitudeDelta = (targetLongitude - longitude) * Math.PI / 180;
            var calculation = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) + Math.Cos(latitude * Math.PI / 180) * Math.Cos(targetLatitude * Math.PI / 180) * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
            return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(calculation), Math.Sqrt(1 - calculation));
        }

        private class OpenPriceResponse { public List<OpenPrice> Items { get; set; } = new List<OpenPrice>(); }
        private class OpenPrice
        {
            public int Id { get; set; }
            [JsonPropertyName("product_id")] public int ProductId { get; set; }
            [JsonPropertyName("product_code")] public string ProductCode { get; set; }
            [JsonPropertyName("product_name")] public string ProductName { get; set; }
            public decimal? Price { get; set; }
            [JsonPropertyName("price_is_discounted")] public bool PriceIsDiscounted { get; set; }
            [JsonPropertyName("price_without_discount")] public decimal? PriceWithoutDiscount { get; set; }
            public string Currency { get; set; }
            public DateTime? Date { get; set; }
            public OpenPriceProduct Product { get; set; }
            public OpenPriceLocation Location { get; set; }
        }
        private class OpenPriceProduct
        {
            [JsonPropertyName("product_name")] public string ProductName { get; set; }
            [JsonPropertyName("image_url")] public string ImageUrl { get; set; }
        }
        private class OpenPriceLocation
        {
            public int Id { get; set; }
            [JsonPropertyName("osm_name")] public string OsmName { get; set; }
            [JsonPropertyName("osm_display_name")] public string OsmDisplayName { get; set; }
            [JsonPropertyName("osm_brand")] public string OsmBrand { get; set; }
            [JsonPropertyName("osm_address_city")] public string OsmAddressCity { get; set; }
            [JsonPropertyName("osm_address_postcode")] public string OsmAddressPostcode { get; set; }
            [JsonPropertyName("osm_address_country_code")] public string OsmAddressCountryCode { get; set; }
            [JsonPropertyName("osm_lat")] public double? OsmLat { get; set; }
            [JsonPropertyName("osm_lon")] public double? OsmLon { get; set; }
        }
    }
}