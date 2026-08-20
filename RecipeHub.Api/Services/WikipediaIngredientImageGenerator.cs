using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class WikipediaIngredientImageGenerator : IIngredientImageGenerator
    {
        private static readonly Dictionary<string, string[]> KnownAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["natural skyr"] = new[] { "Skyr" },
            ["kiwi"] = new[] { "Kiwifruit" },
            ["myslidrys"] = new[] { "Muesli", "Granola" },
            ["mustard"] = new[] { "Mustard (condiment)", "Mustard" },
            ["basil leaves"] = new[] { "Basil" },
            ["salt & pepper"] = new[] { "Black pepper", "Salt" },
            ["big spidskål leaves"] = new[] { "Cabbage", "Savoy cabbage" },
            ["yellow pepperfruit"] = new[] { "Bell pepper" },
            ["skummet milk"] = new[] { "Skimmed milk" },
            ["tymian"] = new[] { "Thyme" },
            ["red bell pepper"] = new[] { "Bell pepper" },
            ["pepper"] = new[] { "Black pepper" },
            ["white whine vinegar"] = new[] { "White wine vinegar", "Vinegar" },
            ["chicken fond"] = new[] { "Stock (food)", "Broth" },
            ["milk ricotta"] = new[] { "Ricotta" },
            ["red chilli"] = new[] { "Chili pepper" },
            ["lime"] = new[] { "Lime (fruit)" }
        };

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WikipediaIngredientImageGenerator> _logger;

        public WikipediaIngredientImageGenerator(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<WikipediaIngredientImageGenerator> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ImageViewModel> GenerateAsync(string ingredientName, string description)
        {
            var section = _configuration.GetSection("AIImageGeneration");
            var enabled = bool.TryParse(section["Enabled"], out var parsedEnabled) && parsedEnabled;
            if (!enabled)
            {
                return null;
            }

            var safeIngredientName = string.IsNullOrWhiteSpace(ingredientName) ? "ingredient" : ingredientName.Trim();
            foreach (var candidate in BuildCandidates(safeIngredientName))
            {
                var imageUrl = await TryGetImageUrlFromWikipedia(candidate);
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    continue;
                }

                return new ImageViewModel
                {
                    Url = imageUrl,
                    Caption = $"Image for {safeIngredientName} (Wikipedia)"
                };
            }

            _logger.LogInformation("No free Wikipedia image found for ingredient {IngredientName}", safeIngredientName);
            return null;
        }

        private async Task<string> TryGetImageUrlFromWikipedia(string title)
        {
            var direct = await TryGetImageUrlFromSummary(title);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            var searchTitles = await SearchWikipediaTitles(title);
            foreach (var foundTitle in searchTitles)
            {
                var imageUrl = await TryGetImageUrlFromSummary(foundTitle);
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return imageUrl;
                }
            }

            return null;
        }

        private async Task<string> TryGetImageUrlFromSummary(string title)
        {
            var pageTitle = WebUtility.UrlEncode(title.Replace(' ', '_'));

            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://en.wikipedia.org/api/rest_v1/page/summary/{pageTitle}");
            request.Headers.TryAddWithoutValidation("User-Agent", "RecipeHub/1.0 (ingredient-image-generator)");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            if (json.RootElement.TryGetProperty("thumbnail", out var thumbnail) &&
                thumbnail.TryGetProperty("source", out var source))
            {
                return source.GetString();
            }

            if (json.RootElement.TryGetProperty("originalimage", out var originalImage) &&
                originalImage.TryGetProperty("source", out var originalSource))
            {
                return originalSource.GetString();
            }

            return null;
        }

        private async Task<IEnumerable<string>> SearchWikipediaTitles(string query)
        {
            var encodedQuery = WebUtility.UrlEncode(query);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={encodedQuery}&format=json&srlimit=5");
            request.Headers.TryAddWithoutValidation("User-Agent", "RecipeHub/1.0 (ingredient-image-generator)");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return Enumerable.Empty<string>();
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            if (!json.RootElement.TryGetProperty("query", out var queryNode) ||
                !queryNode.TryGetProperty("search", out var searchNode) ||
                searchNode.ValueKind != JsonValueKind.Array)
            {
                return Enumerable.Empty<string>();
            }

            var titles = new List<string>();
            foreach (var item in searchNode.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var titleNode))
                {
                    var title = titleNode.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        titles.Add(title);
                    }
                }
            }

            return titles;
        }

        private static IEnumerable<string> BuildCandidates(string ingredientName)
        {
            var candidates = new List<string>();
            var cleaned = ingredientName.Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return new[] { "ingredient" };
            }

            candidates.Add(cleaned);

            var noParenthesis = cleaned.Split('(')[0].Trim();
            if (!string.Equals(noParenthesis, cleaned, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(noParenthesis);
            }

            if (KnownAliases.TryGetValue(noParenthesis, out var aliases))
            {
                candidates.AddRange(aliases);
            }

            if (KnownAliases.TryGetValue(cleaned, out var exactAliases))
            {
                candidates.AddRange(exactAliases);
            }

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "shredded", "chopped", "sliced", "diced", "fresh", "small", "large", "ground", "minced"
            };

            var filteredWords = noParenthesis
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !stopWords.Contains(word))
                .ToArray();

            if (filteredWords.Length > 0)
            {
                var filtered = string.Join(" ", filteredWords);
                candidates.Add(filtered);

                if (KnownAliases.TryGetValue(filtered, out var filteredAliases))
                {
                    candidates.AddRange(filteredAliases);
                }

                var lastWord = filteredWords[^1];
                var singular = lastWord.EndsWith("s", StringComparison.OrdinalIgnoreCase) && lastWord.Length > 3
                    ? lastWord.Substring(0, lastWord.Length - 1)
                    : lastWord;

                if (!string.Equals(singular, lastWord, StringComparison.OrdinalIgnoreCase))
                {
                    var singularWords = filteredWords.ToArray();
                    singularWords[^1] = singular;
                    candidates.Add(string.Join(" ", singularWords));
                    candidates.Add(singular);
                }
            }

            return candidates
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}