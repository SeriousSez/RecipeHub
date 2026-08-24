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
using System.Text.Json;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class CamGozGroceryProvider : IGroceryProvider
    {
        private static readonly IReadOnlyDictionary<string, string> TurkishIngredientQueries =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apple"] = "elma",
                ["apples"] = "elma",
                ["beef"] = "dana eti",
                ["butter"] = "tereyağı",
                ["carrot"] = "havuç",
                ["carrots"] = "havuç",
                ["cheese"] = "peynir",
                ["chicken"] = "tavuk",
                ["chicken breast"] = "tavuk göğsü",
                ["cream"] = "krema",
                ["cucumber"] = "salatalık",
                ["egg"] = "yumurta",
                ["eggs"] = "yumurta",
                ["flour"] = "un",
                ["garlic"] = "sarımsak",
                ["lemon"] = "limon",
                ["lemons"] = "limon",
                ["milk"] = "süt",
                ["mushroom"] = "mantar",
                ["mushrooms"] = "mantar",
                ["olive oil"] = "zeytinyağı",
                ["onion"] = "soğan",
                ["onions"] = "soğan",
                ["potato"] = "patates",
                ["potatoes"] = "patates",
                ["rice"] = "pirinç",
                ["salt"] = "tuz",
                ["spinach"] = "ıspanak",
                ["sugar"] = "şeker",
                ["tomato"] = "domates",
                ["tomatoes"] = "domates",
                ["yogurt"] = "yoğurt"
            };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CamGozGroceryProvider> _logger;

        public CamGozGroceryProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache, ILogger<CamGozGroceryProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public string CountryCode => "TR";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["CamGoz:ApiKey"]);

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var searches = ingredients.Select(async ingredient => new { Ingredient = ingredient, Products = await SearchAsync(GetQuery(ingredient), model.ForceRefresh) }).ToList();
            var results = await Task.WhenAll(searches);
            var offers = new List<GroceryIngredientOfferViewModel>();

            foreach (var result in results)
            {
                foreach (var product in result.Products.Take(5))
                {
                    foreach (var market in product.Markets.Where(value => value.StockAvailable && GetPrice(value) > 0).OrderBy(GetPrice).Take(5))
                    {
                        var price = GetPrice(market);
                        var originalPrice = market.DiscountPrice > 0 && market.Price > market.DiscountPrice ? market.Price : (decimal?)null;
                        offers.Add(new GroceryIngredientOfferViewModel
                        {
                            IngredientName = result.Ingredient,
                            ProductName = product.Name,
                            ProductId = product.Barcode,
                            OfferId = $"{result.Ingredient}|{product.Barcode}|{market.Id}",
                            ProductUrl = market.SourceUrl,
                            ChainName = market.Market,
                            StoreName = market.Market,
                            StoreAddress = market.Location,
                            DistanceKm = 0,
                            Price = price,
                            OriginalPrice = originalPrice,
                            DiscountPercentage = originalPrice > 0 ? (int?)Math.Round((originalPrice.Value - price) / originalPrice.Value * 100) : null,
                            Currency = "TRY",
                            PriceKind = originalPrice.HasValue ? "campaign" : "regular",
                            ValidFrom = market.PriceModified,
                            ImageUrl = product.ImageUrl
                        });
                    }
                }
            }

            var stores = offers.GroupBy(offer => offer.ChainName, StringComparer.OrdinalIgnoreCase).Select(group => new GroceryNearbyStoreViewModel
            {
                Id = group.Key,
                Name = group.Key,
                ChainName = group.Key,
                Address = group.First().StoreAddress,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                DistanceKm = 0
            }).ToList();

            return new GroceryOfferSearchResponse
            {
                Stores = stores,
                Offers = offers,
                UnmatchedIngredients = ingredients.Where(ingredient => !offers.Any(offer => offer.IngredientName.Equals(ingredient, StringComparison.OrdinalIgnoreCase))).ToList(),
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private async Task<List<CamGozProduct>> SearchAsync(string query, bool forceRefresh)
        {
            var cacheKey = $"camgoz:{query.ToLowerInvariant()}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<CamGozProduct> cached)) return cached;

            var client = _httpClientFactory.CreateClient("CamGoz");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/external/search?query={Uri.EscapeDataString(query)}&marketPrices=true&historyPrices=false");
            request.Headers.Add("X-JoJAPI-Key", _configuration["CamGoz:ApiKey"]);
            using var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound) return new List<CamGozProduct>();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CamGoz returned {StatusCode}", (int)response.StatusCode);
                throw new GroceryOfferProviderException((int)response.StatusCode);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var products = await JsonSerializer.DeserializeAsync<List<CamGozProduct>>(stream, JsonOptions) ?? new List<CamGozProduct>();
            _cache.Set(cacheKey, products, TimeSpan.FromHours(1));
            return products;
        }

        private static decimal GetPrice(CamGozMarket market) => market.DiscountPrice > 0 ? market.DiscountPrice.Value : market.Price;

        private static string GetQuery(string ingredient)
        {
            var parenthesisIndex = ingredient.IndexOf('(');
            var normalized = (parenthesisIndex >= 0 ? ingredient.Substring(0, parenthesisIndex) : ingredient).Trim();
            return TurkishIngredientQueries.TryGetValue(normalized, out var translated) ? translated : normalized;
        }

        private class CamGozProduct
        {
            public string Name { get; set; }
            public string Barcode { get; set; }
            public string ImageUrl { get; set; }
            public List<CamGozMarket> Markets { get; set; } = new List<CamGozMarket>();
        }

        private class CamGozMarket
        {
            public string Id { get; set; }
            public decimal Price { get; set; }
            public decimal? DiscountPrice { get; set; }
            public DateTime? PriceModified { get; set; }
            public string Market { get; set; }
            public string SourceUrl { get; set; }
            public string Location { get; set; }
            public bool StockAvailable { get; set; }
        }
    }
}