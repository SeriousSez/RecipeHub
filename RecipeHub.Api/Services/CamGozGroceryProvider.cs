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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RecipeHub.Infrastructure;

namespace RecipeHub.Api.Services
{
    public class CamGozGroceryProvider : IGroceryProvider
    {
        private static readonly IReadOnlyDictionary<string, string> IngredientCategoryHints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tomato"] = "produce",
                ["tomatoes"] = "produce",
                ["tomato paste"] = "pantry",
                ["tomato puree"] = "pantry",
                ["tomato sauce"] = "pantry",
                ["mushroom"] = "produce",
                ["mushrooms"] = "produce",
                ["carrot"] = "produce",
                ["carrots"] = "produce",
                ["onion"] = "produce",
                ["onions"] = "produce",
                ["potato"] = "produce",
                ["potatoes"] = "produce",
                ["garlic"] = "produce",
                ["spinach"] = "produce",
                ["lemon"] = "produce",
                ["lemons"] = "produce",
                ["cucumber"] = "produce",
                ["egg"] = "dairy",
                ["eggs"] = "dairy",
                ["milk"] = "dairy",
                ["yogurt"] = "dairy",
                ["butter"] = "dairy",
                ["cheese"] = "dairy",
                ["flour"] = "pantry",
                ["rice"] = "pantry",
                ["salt"] = "pantry",
                ["sugar"] = "pantry",
                ["olive oil"] = "pantry",
                ["noodle"] = "pantry",
                ["noodles"] = "pantry",
                ["ramen"] = "pantry",
                ["pasta"] = "pantry",
                ["spaghetti"] = "pantry",
            };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CamGozGroceryProvider> _logger;
        private readonly RecipeHubContext _context;
        private readonly IRecipeTranslationService _translationService;

        public CamGozGroceryProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache, ILogger<CamGozGroceryProvider> logger, RecipeHubContext context, IRecipeTranslationService translationService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _context = context;
            _translationService = translationService;
        }

        public string CountryCode => "TR";
        public bool IsConfigured => true;

        public async Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var marketFiyatiResult = await FindMarketFiyatiOffersAsync(model);
            if (marketFiyatiResult?.Offers.Count > 0)
            {
                return marketFiyatiResult;
            }

            if (string.IsNullOrWhiteSpace(_configuration["CamGoz:ApiKey"]))
            {
                return marketFiyatiResult ?? new GroceryOfferSearchResponse
                {
                    UnmatchedIngredients = ingredients,
                    GeneratedAtUtc = DateTime.UtcNow
                };
            }

            var results = new List<(string Ingredient, List<CamGozProduct> Products)>();
            foreach (var ingredient in ingredients)
            {
                var products = await SearchWithFallbackQueriesAsync(ingredient, model.ForceRefresh);
                if (products == null)
                {
                    break;
                }

                results.Add((ingredient, products));
            }
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

        private async Task<GroceryOfferSearchResponse> FindMarketFiyatiOffersAsync(GroceryOfferSearchViewModel model)
        {
            var ingredients = model.IngredientNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var offers = new List<GroceryIngredientOfferViewModel>();
            var client = _httpClientFactory.CreateClient("MarketFiyati");
            var marketCategories = await GetMarketFiyatiCategoriesAsync(client, model.ForceRefresh);
            var translatedIngredients = await _translationService.TranslateIngredientNamesAsync(ingredients, "Turkish", model.IngredientContexts)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ingredient in ingredients)
            {
                try
                {
                    var category = GetIngredientCategory(ingredient, model);
                    var categoryPreference = await GetCategoryPreferenceAsync(ingredient);
                    var hasApprovedCategory = categoryPreference.Values.Any(preference => preference > 0);
                    if (category == "auto")
                    {
                        category = categoryPreference
                            .Where(item => item.Value > 0)
                            .OrderByDescending(item => item.Value)
                            .Select(item => item.Key)
                            .FirstOrDefault() ?? "auto";
                    }
                    var translatedIngredient = translatedIngredients.TryGetValue(ingredient, out var translated) ? translated : null;
                    var queries = GetQueryVariants(ingredient, category, translatedIngredient);
                    var products = new List<MarketFiyatiProduct>();
                    foreach (var query in queries)
                    {
                        var requestBody = new
                        {
                            keywords = query,
                            pages = 0,
                            size = 24,
                            latitude = model.Latitude,
                            longitude = model.Longitude,
                            distance = model.RadiusKm
                        };

                        using var request = new HttpRequestMessage(HttpMethod.Post, "search")
                        {
                            Content = JsonContent.Create(requestBody)
                        };

                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
                        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("tr-TR"));
                        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
                        request.Headers.TryAddWithoutValidation("Origin", "https://marketfiyati.org.tr");
                        request.Headers.TryAddWithoutValidation("Referer", "https://marketfiyati.org.tr/");

                        using var response = await client.SendAsync(request);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Market Fiyatı returned {StatusCode}; falling back to CamGoz", (int)response.StatusCode);
                            return null;
                        }

                        var searchResult = await response.Content.ReadFromJsonAsync<MarketFiyatiSearchResult>(JsonOptions);
                        var foundProducts = searchResult?.Content ?? new List<MarketFiyatiProduct>();
                        if (foundProducts.Count > 0)
                        {
                            products = foundProducts;
                            break;
                        }
                    }

                    var rankedProducts = products
                        .Select(product => new
                        {
                            Product = product,
                            Score = ScoreMarketFiyatiProduct(product, ingredient, category),
                            CategoryPreference = categoryPreference.TryGetValue(NormalizeFeedbackValue(GetMarketFiyatiCategory(product, marketCategories)), out var preference) ? preference : 0
                        })
                        .Where(item => item.Score > 0)
                        .Where(item => categoryPreference.TryGetValue(NormalizeFeedbackValue(GetMarketFiyatiCategory(item.Product, marketCategories)), out var preference)
                            ? hasApprovedCategory ? preference > 0 : preference >= 0
                            : !hasApprovedCategory)
                        .OrderByDescending(item => item.CategoryPreference)
                        .ThenByDescending(item => item.Score)
                        .ThenBy(item => item.Product.Title, StringComparer.OrdinalIgnoreCase)
                        .Select(item => item.Product)
                        .ToList();
                    var fallbackProducts = products
                        .Select(product => new
                        {
                            Product = product,
                            Score = ScoreMarketFiyatiProduct(product, ingredient, category),
                            CategoryPreference = categoryPreference.TryGetValue(NormalizeFeedbackValue(GetMarketFiyatiCategory(product, marketCategories)), out var preference) ? preference : 0
                        })
                        .Where(item => item.Score > 0 && categoryPreference.TryGetValue(NormalizeFeedbackValue(GetMarketFiyatiCategory(item.Product, marketCategories)), out var preference) && preference < 0)
                        .OrderByDescending(item => item.CategoryPreference)
                        .ThenByDescending(item => item.Score)
                        .Select(item => item.Product)
                        .ToList();
                    rankedProducts = rankedProducts.Take(5).Concat(fallbackProducts.Take(Math.Max(0, 5 - rankedProducts.Count))).ToList();

                    foreach (var product in rankedProducts)
                    {
                        foreach (var market in product.ProductDepotInfoList.Where(value => GetPrice(value) > 0).OrderBy(GetPrice).Take(5))
                        {
                            var price = GetPrice(market);
                            offers.Add(new GroceryIngredientOfferViewModel
                            {
                                IngredientName = ingredient,
                                ProductCategory = GetMarketFiyatiCategory(product, marketCategories),
                                ProductName = product.Title ?? ingredient,
                                ProductId = product.Id ?? product.Title ?? ingredient,
                                OfferId = $"{ingredient}|marketfiyati|{market.Id}",
                                ProductUrl = null,
                                ChainName = market.MarketAdi,
                                StoreName = market.DepotName ?? market.MarketAdi,
                                StoreAddress = market.DepotName,
                                DistanceKm = market.Latitude.HasValue && market.Longitude.HasValue
                                    ? CalculateDistanceKm(model.Latitude, model.Longitude, market.Latitude.Value, market.Longitude.Value)
                                    : 0,
                                Price = price,
                                OriginalPrice = market.Discount && market.DiscountPrice > price ? market.Price : null,
                                DiscountPercentage = market.DiscountPrice > 0 && market.Price > market.DiscountPrice
                                    ? (int?)Math.Round((market.Price - market.DiscountPrice.Value) / market.Price * 100)
                                    : null,
                                Currency = "TRY",
                                PriceKind = market.DiscountPrice > 0 && market.DiscountPrice < market.Price ? "campaign" : "regular",
                                ValidFrom = ParseMarketDate(market.IndexTime),
                                ImageUrl = product.ImageUrl
                            });
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Market Fiyatı search failed; falling back to CamGoz");
                    return null;
                }
            }

            return new GroceryOfferSearchResponse
            {
                Offers = offers,
                Stores = offers.GroupBy(offer => offer.StoreName, StringComparer.OrdinalIgnoreCase).Select(group => new GroceryNearbyStoreViewModel
                {
                    Id = group.Key,
                    Name = group.Key,
                    ChainName = group.Key,
                    Address = group.First().StoreAddress,
                    Latitude = model.Latitude,
                    Longitude = model.Longitude
                }).ToList(),
                AvailableCategories = marketCategories,
                IngredientDisplayNames = translatedIngredients.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                UnmatchedIngredients = ingredients.Where(ingredient => !offers.Any(offer => offer.IngredientName.Equals(ingredient, StringComparison.OrdinalIgnoreCase))).ToList(),
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private static string GetIngredientCategory(string ingredient, GroceryOfferSearchViewModel model)
        {
            if (model?.IngredientCategories != null && model.IngredientCategories.TryGetValue(ingredient, out var category) && !string.IsNullOrWhiteSpace(category))
            {
                return category.Trim();
            }

            if (IngredientCategoryHints.TryGetValue(ingredient, out var hint))
            {
                return hint;
            }

            var normalized = NormalizeIngredientName(ingredient);
            if (IngredientCategoryHints.TryGetValue(normalized, out var normalizedHint))
            {
                return normalizedHint;
            }

            return "auto";
        }

        private static int ScoreMarketFiyatiProduct(MarketFiyatiProduct product, string ingredient, string category)
        {
            var title = product?.Title ?? string.Empty;
            var normalizedTitle = NormalizeComparisonText(title);
            var normalizedIngredient = NormalizeComparisonText(ingredient);
            var score = 0;

            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return 0;
            }

            if (normalizedTitle.Contains(normalizedIngredient, StringComparison.Ordinal))
            {
                score += 40;
            }

            if (!string.Equals(category, "auto", StringComparison.OrdinalIgnoreCase) &&
                !new[] { "produce", "dairy", "meat", "bakery", "pantry", "candy", "chocolate", "beverages" }.Contains(category, StringComparer.OrdinalIgnoreCase) &&
                normalizedTitle.Contains(NormalizeComparisonText(category), StringComparison.Ordinal))
            {
                score += 20;
            }

            var translatedIngredient = ingredient;

            var normalizedTranslatedIngredient = NormalizeComparisonText(translatedIngredient);
            if (!string.IsNullOrWhiteSpace(normalizedTranslatedIngredient) && normalizedTitle.Contains(normalizedTranslatedIngredient, StringComparison.Ordinal))
            {
                score += 25;
            }

            if (category == "produce")
            {
                if (normalizedTitle.Contains("domates", StringComparison.Ordinal) || normalizedTitle.Contains("salatalik", StringComparison.Ordinal) || normalizedTitle.Contains("patates", StringComparison.Ordinal) || normalizedTitle.Contains("havuç", StringComparison.Ordinal))
                {
                    score += 15;
                }

                if (normalizedTitle.Contains("salça", StringComparison.Ordinal) || normalizedTitle.Contains("sos", StringComparison.Ordinal) || normalizedTitle.Contains("püre", StringComparison.Ordinal) || normalizedTitle.Contains("konserve", StringComparison.Ordinal))
                {
                    score -= 50;
                }
            }

            if (category == "pantry")
            {
                if (normalizedTitle.Contains("makarna", StringComparison.Ordinal) || normalizedTitle.Contains("un", StringComparison.Ordinal) || normalizedTitle.Contains("pirinç", StringComparison.Ordinal))
                {
                    score += 15;
                }

                if (normalizedTitle.Contains("instant", StringComparison.Ordinal) || normalizedTitle.Contains("cup", StringComparison.Ordinal) || normalizedTitle.Contains("çorba", StringComparison.Ordinal))
                {
                    score -= 35;
                }
            }

            if (category == "auto")
            {
                if (normalizedTitle.Contains("salça", StringComparison.Ordinal) || normalizedTitle.Contains("sos", StringComparison.Ordinal) || normalizedTitle.Contains("püre", StringComparison.Ordinal))
                {
                    score -= 15;
                }
            }

            if (normalizedTitle.Contains("taze", StringComparison.Ordinal) || normalizedTitle.Contains("fresh", StringComparison.Ordinal))
            {
                score += category == "produce" ? 10 : 0;
            }

            return score;
        }

        private static string NormalizeIngredientName(string ingredient)
        {
            var parenthesisIndex = ingredient.IndexOf('(');
            var value = parenthesisIndex >= 0 ? ingredient.Substring(0, parenthesisIndex) : ingredient;
            return value.Trim();
        }

        private static string NormalizeComparisonText(string value)
        {
            return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                .ToArray());
        }

        private static decimal GetPrice(MarketFiyatiDepot market) => market.Discount && market.DiscountPrice > 0 ? market.DiscountPrice.Value : market.Price;

        private static DateTime? ParseMarketDate(string value) => DateTime.TryParseExact(value, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

        private static double CalculateDistanceKm(double latitude, double longitude, double targetLatitude, double targetLongitude)
        {
            const double earthRadiusKm = 6371;
            var latitudeDelta = (targetLatitude - latitude) * Math.PI / 180;
            var longitudeDelta = (targetLongitude - longitude) * Math.PI / 180;
            var calculation = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) + Math.Cos(latitude * Math.PI / 180) * Math.Cos(targetLatitude * Math.PI / 180) * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
            return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(calculation), Math.Sqrt(1 - calculation));
        }

        private async Task<List<CamGozProduct>> SearchAsync(string query, bool forceRefresh)
        {
            var cacheKey = $"camgoz:{query.ToLowerInvariant()}";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<CamGozProduct> cached)) return cached;

            var client = _httpClientFactory.CreateClient("CamGoz");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/external/search?query={Uri.EscapeDataString(query)}&marketPrices=true&historyPrices=false");
            request.Headers.Add("X-JoJAPI-Key", _configuration["CamGoz:ApiKey"]);
            using var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                _logger.LogWarning("CamGoz returned {StatusCode}; stopping the search and preserving earlier matches", (int)response.StatusCode);
                return response.StatusCode == HttpStatusCode.PaymentRequired ? null : new List<CamGozProduct>();
            }
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

        private async Task<Dictionary<string, int>> GetCategoryPreferenceAsync(string ingredient)
        {
            var normalizedIngredient = NormalizeFeedbackValue(ingredient);
            return await _context.GroceryCategoryFeedback
                .AsNoTracking()
                .Where(feedback => feedback.IngredientName == normalizedIngredient)
                .ToDictionaryAsync(feedback => feedback.Category, feedback => feedback.ApprovalCount - feedback.RejectionCount, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<string>> GetMarketFiyatiCategoriesAsync(HttpClient client, bool forceRefresh)
        {
            const string cacheKey = "marketfiyati:categories";
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<string> cached)) return cached;

            try
            {
                using var response = await client.GetAsync("https://api.marketfiyati.org.tr/api/v3/info/categories");
                if (!response.IsSuccessStatusCode) return new List<string>();

                var result = await response.Content.ReadFromJsonAsync<MarketFiyatiCategoryResponse>(JsonOptions);
                var categories = FlattenMarketFiyatiCategories(result?.Content).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                _cache.Set(cacheKey, categories, TimeSpan.FromHours(12));
                return categories;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Market Fiyatı categories could not be loaded; using title fallback");
                return new List<string>();
            }
        }

        private static IEnumerable<string> FlattenMarketFiyatiCategories(IEnumerable<MarketFiyatiCategory> categories)
        {
            foreach (var category in categories ?? Enumerable.Empty<MarketFiyatiCategory>())
            {
                if (!string.IsNullOrWhiteSpace(category.Name)) yield return category.Name;
                foreach (var child in FlattenMarketFiyatiCategories(category.Children)) yield return child;
            }
        }

        private static string GetMarketFiyatiCategory(MarketFiyatiProduct product, IReadOnlyList<string> categories)
        {
            var title = NormalizeComparisonText(product?.Title);
            var catalogCategory = (categories ?? new List<string>())
                .OrderByDescending(category => NormalizeComparisonText(category).Length)
                .FirstOrDefault(category => NormalizeComparisonText(category).Length > 2 && title.Contains(NormalizeComparisonText(category), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(catalogCategory)) return catalogCategory;
            if (title.Contains("salca", StringComparison.Ordinal) || title.Contains("sos", StringComparison.Ordinal) || title.Contains("pure", StringComparison.Ordinal)) return "pantry";
            if (title.Contains("instant", StringComparison.Ordinal) || title.Contains("cup", StringComparison.Ordinal)) return "pantry";
            if (title.Contains("domates", StringComparison.Ordinal) || title.Contains("havuc", StringComparison.Ordinal) || title.Contains("patates", StringComparison.Ordinal)) return "produce";
            return "auto";
        }

        private static decimal GetPrice(CamGozMarket market) => market.DiscountPrice > 0 ? market.DiscountPrice.Value : market.Price;

        private async Task<List<CamGozProduct>> SearchWithFallbackQueriesAsync(string ingredient, bool forceRefresh)
        {
            var queries = GetQueryVariants(ingredient);
            foreach (var query in queries)
            {
                var products = await SearchAsync(query, forceRefresh);
                if (products != null && products.Count > 0)
                {
                    return products;
                }
            }

            return new List<CamGozProduct>();
        }

        private static string NormalizeFeedbackValue(string value) => string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static IReadOnlyList<string> GetQueryVariants(string ingredient, string category = "auto", string translatedIngredient = null)
        {
            var normalized = NormalizeIngredientName(ingredient);

            if (!string.IsNullOrWhiteSpace(translatedIngredient))
            {
                var translatedVariants = new List<string> { translatedIngredient.Trim() };
                translatedVariants.Add(GetPluralQuery(translatedIngredient));
                if (category == "produce" && normalized.Equals("tomato", StringComparison.OrdinalIgnoreCase)) translatedVariants.Insert(0, "taze " + translatedIngredient.Trim());
                return translatedVariants.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            if (normalized.Equals("tomato", StringComparison.OrdinalIgnoreCase) || normalized.Equals("tomatoes", StringComparison.OrdinalIgnoreCase))
            {
                var variants = category == "produce"
                    ? new[] { "taze domates", "domates", "domates salçası", "domates sosu", "domates püresi" }
                    : new[] { "domates", "domates salçası", "domates sosu", "domates püresi", "taze domates" };

                return variants.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            if (normalized.Equals("noodles", StringComparison.OrdinalIgnoreCase) || normalized.Equals("noodle", StringComparison.OrdinalIgnoreCase) || normalized.Equals("pasta", StringComparison.OrdinalIgnoreCase))
            {
                var variants = category == "pantry"
                    ? new[] { "makarna", "spagetti", "erişte", "noddles", "instant noodles" }
                    : new[] { "makarna", "instant noodle", "spagetti", "erişte" };

                return variants.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return new[] { normalized };
        }

        private static string GetPluralQuery(string query)
        {
            var normalized = query?.Trim() ?? string.Empty;
            if (normalized.Length == 0 || normalized.EndsWith("lar", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("ler", StringComparison.OrdinalIgnoreCase)) return normalized;
            var lastVowel = normalized.LastOrDefault(character => "aeıioöuü".Contains(character));
            return normalized + ("aıou".Contains(lastVowel) ? "lar" : "ler");
        }

        private static string GetQuery(string ingredient)
        {
            return GetQueryVariants(ingredient).FirstOrDefault() ?? ingredient;
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

        private class MarketFiyatiSearchResult
        {
            public List<MarketFiyatiProduct> Content { get; set; } = new List<MarketFiyatiProduct>();
        }

        private class MarketFiyatiCategoryResponse
        {
            public List<MarketFiyatiCategory> Content { get; set; } = new List<MarketFiyatiCategory>();
        }

        private class MarketFiyatiCategory
        {
            public string Name { get; set; }
            public List<MarketFiyatiCategory> Children { get; set; } = new List<MarketFiyatiCategory>();
        }

        private class MarketFiyatiProduct
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string ImageUrl { get; set; }
            public List<MarketFiyatiDepot> ProductDepotInfoList { get; set; } = new List<MarketFiyatiDepot>();
        }

        private class MarketFiyatiDepot
        {
            public string Id { get; set; }
            public decimal Price { get; set; }
            public decimal? DiscountPrice { get; set; }
            public string MarketAdi { get; set; }
            public string DepotName { get; set; }
            public string IndexTime { get; set; }
            public bool Discount { get; set; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
        }
    }
}