using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public interface IRecipeTranslationService
    {
        Task<RecipeResponse> TranslateAsync(RecipeResponse recipe, string targetLanguage);
        Task<List<RecipeResponse>> TranslateSummariesAsync(List<RecipeResponse> recipes, string targetLanguage);
        Task<IReadOnlyDictionary<string, string>> CanonicalizeIngredientNamesAsync(IEnumerable<string> names, string sourceLanguage);
        Task<IReadOnlyDictionary<string, string>> TranslateIngredientNamesAsync(IEnumerable<string> names, string targetLanguage);
    }

    public class OpenAiRecipeTranslationService : IRecipeTranslationService
    {
        private static readonly Regex InstructionMarkupPattern = new Regex("(<[^>]+>|&(?:#\\d+|#x[0-9A-Fa-f]+|[A-Za-z]+);)", RegexOptions.Compiled);
        private static readonly HashSet<string> SupportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Danish", "English", "Estonian", "Turkish"
        };
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CommonIngredientTranslations =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Danish"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["garlic"] = "hvidløg",
                    ["onion"] = "løg",
                    ["salt"] = "salt",
                    ["black pepper"] = "sort peber",
                    ["mushrooms"] = "svampe",
                    ["butter"] = "smør",
                    ["olive oil"] = "olivenolie",
                    ["parsley"] = "persille"
                },
                ["Estonian"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["garlic"] = "küüslauk",
                    ["onion"] = "sibul",
                    ["salt"] = "sool",
                    ["black pepper"] = "must pipar",
                    ["mushrooms"] = "seened",
                    ["butter"] = "või",
                    ["olive oil"] = "oliiviõli",
                    ["parsley"] = "petersell"
                },
                ["Turkish"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["garlic"] = "sarımsak",
                    ["onion"] = "soğan",
                    ["salt"] = "tuz",
                    ["black pepper"] = "karabiber",
                    ["mushrooms"] = "mantar",
                    ["butter"] = "tereyağı",
                    ["olive oil"] = "zeytinyağı",
                    ["parsley"] = "maydanoz"
                }
            };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OpenAiRecipeTranslationService> _logger;

        public OpenAiRecipeTranslationService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<OpenAiRecipeTranslationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public async Task<RecipeResponse> TranslateAsync(RecipeResponse recipe, string targetLanguage)
        {
            var language = SupportedLanguages.FirstOrDefault(item => item.Equals(targetLanguage?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (recipe == null || language == null || language.Equals("English", StringComparison.OrdinalIgnoreCase) ||
                language.Equals(recipe.Language, StringComparison.OrdinalIgnoreCase))
            {
                return recipe;
            }

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? _configuration["RecipeTranslation:OpenAIApiKey"]
                ?? _configuration["NutritionEstimation:OpenAIApiKey"]
                ?? _configuration["AIImageGeneration:OpenAIApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Recipe translation is enabled, but no OpenAI API key is available");
                return recipe;
            }

            var source = new TranslationPayload
            {
                Title = recipe.Title,
                Description = recipe.Description,
                InstructionSegments = ExtractInstructionSegments(recipe.Instructions),
                Portions = recipe.Portions,
                ImageCaption = recipe.Image?.Caption,
                Ingredients = (recipe.Ingredients ?? new List<IngredientResponse>()).Select(ingredient => new IngredientTranslation
                {
                    Name = ingredient.Name,
                    Description = ingredient.Description,
                    AmountType = ingredient.AmountType,
                    Group = ingredient.Group
                }).ToList()
            };
            var sourceJson = JsonSerializer.Serialize(source);
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceJson)));
            var cacheKey = $"recipe-translation:{recipe.Id}:{language.ToLowerInvariant()}:{sourceHash}";
            if (_cache.TryGetValue(cacheKey, out RecipeResponse cachedRecipe)) return cachedRecipe;

            var endpoint = _configuration["RecipeTranslation:OpenAIEndpoint"] ?? "https://api.openai.com/v1/chat/completions";
            var model = _configuration["RecipeTranslation:OpenAIModel"] ?? "gpt-4o-mini";
            var requestBody = new
            {
                model,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = "You translate recipes accurately. Return valid JSON only. Never alter numbers, array order, or add content." },
                    new { role = "user", content = $"Translate every string value in this recipe JSON from English to {language}. Keep empty values empty and return the identical JSON shape: {sourceJson}" }
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(requestBody);
                using var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Recipe translation failed with status {StatusCode}", (int)response.StatusCode);
                    return recipe;
                }

                using var document = JsonDocument.Parse(responseBody);
                var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                var translation = JsonSerializer.Deserialize<TranslationPayload>(content ?? string.Empty, JsonOptions);
                if (translation?.Ingredients == null || translation.Ingredients.Count != source.Ingredients.Count ||
                    translation.InstructionSegments == null || translation.InstructionSegments.Count != source.InstructionSegments.Count ||
                    translation.InstructionSegments.Select((segment, index) => segment.Index != index).Any(invalid => invalid)) return recipe;

                var translatedRecipe = CloneRecipe(recipe);
                translatedRecipe.Title = translation.Title ?? recipe.Title;
                translatedRecipe.Description = translation.Description ?? recipe.Description;
                translatedRecipe.Instructions = ApplyInstructionTranslation(recipe.Instructions, translation.InstructionSegments);
                translatedRecipe.Portions = translation.Portions ?? recipe.Portions;
                if (translatedRecipe.Image != null) translatedRecipe.Image.Caption = translation.ImageCaption ?? translatedRecipe.Image.Caption;
                translatedRecipe.Language = language;
                for (var index = 0; index < translatedRecipe.Ingredients.Count; index++)
                {
                    var translatedIngredient = translation.Ingredients[index];
                    translatedRecipe.Ingredients[index].Name = translatedIngredient.Name ?? translatedRecipe.Ingredients[index].Name;
                    translatedRecipe.Ingredients[index].Description = translatedIngredient.Description ?? translatedRecipe.Ingredients[index].Description;
                    translatedRecipe.Ingredients[index].AmountType = translatedIngredient.AmountType ?? translatedRecipe.Ingredients[index].AmountType;
                    translatedRecipe.Ingredients[index].Group = translatedIngredient.Group ?? translatedRecipe.Ingredients[index].Group;
                    translatedRecipe.Ingredients[index].Language = language;
                }

                _cache.Set(cacheKey, translatedRecipe, TimeSpan.FromDays(30));
                return translatedRecipe;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Recipe translation failed for recipe {RecipeId} and language {Language}", recipe.Id, language);
                return recipe;
            }
        }

        public async Task<List<RecipeResponse>> TranslateSummariesAsync(List<RecipeResponse> recipes, string targetLanguage)
        {
            var language = SupportedLanguages.FirstOrDefault(item => item.Equals(targetLanguage?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (recipes == null || recipes.Count == 0 || language == null || language.Equals("English", StringComparison.OrdinalIgnoreCase))
            {
                return recipes;
            }

            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return recipes;

            var source = recipes.Select(recipe => new SummaryTranslation
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description
            }).ToList();
            var sourceJson = JsonSerializer.Serialize(source);
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceJson)));
            var cacheKey = $"recipe-summary-translations:{language.ToLowerInvariant()}:{sourceHash}";
            if (_cache.TryGetValue(cacheKey, out List<RecipeResponse> cachedRecipes)) return cachedRecipes;

            var requestBody = CreateRequestBody(
                $"Translate the title and description values in this JSON array from English to {language}. Keep each id unchanged and return the identical JSON array inside an object with a recipes property: {sourceJson}",
                "You translate recipe summaries accurately. Return valid JSON only. Never alter IDs, numbers, array order, or add content.");

            try
            {
                var content = await SendAsync(requestBody, apiKey);
                if (content == null) return recipes;

                var translation = JsonSerializer.Deserialize<SummaryTranslationResponse>(content, JsonOptions);
                if (translation?.Recipes == null || translation.Recipes.Count != recipes.Count) return recipes;

                var translatedById = translation.Recipes.ToDictionary(item => item.Id);
                var translatedRecipes = recipes.Select(recipe =>
                {
                    var translatedRecipe = CloneRecipe(recipe);
                    if (translatedById.TryGetValue(recipe.Id, out var translated))
                    {
                        translatedRecipe.Title = translated.Title ?? recipe.Title;
                        translatedRecipe.Description = translated.Description ?? recipe.Description;
                        translatedRecipe.Language = language;
                    }
                    return translatedRecipe;
                }).ToList();

                _cache.Set(cacheKey, translatedRecipes, TimeSpan.FromDays(30));
                return translatedRecipes;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Recipe summary translation failed for language {Language}", language);
                return recipes;
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> CanonicalizeIngredientNamesAsync(IEnumerable<string> names, string sourceLanguage)
        {
            var language = SupportedLanguages.FirstOrDefault(item => item.Equals(sourceLanguage?.Trim(), StringComparison.OrdinalIgnoreCase));
            var distinctNames = (names ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctNames.Count == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (language == null)
                return null;

            if (language.Equals("English", StringComparison.OrdinalIgnoreCase))
                return distinctNames.ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);

            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return CreateCommonIngredientTranslations(distinctNames, language);

            var source = distinctNames.Select((name, index) => new IngredientCanonicalization
            {
                Index = index,
                OriginalName = name,
                CanonicalName = name
            }).ToList();
            var sourceJson = JsonSerializer.Serialize(source);
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceJson)));
            var cacheKey = $"ingredient-canonicalization:v1:{language.ToLowerInvariant()}:{sourceHash}";
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, string> cachedNames)) return cachedNames;

            var requestBody = CreateRequestBody(
                $"Convert every canonicalName in this JSON array from {language} to a concise, singular English grocery ingredient name. Preserve index and originalName exactly. Text that is already English must remain English. Return an object with an ingredients property containing the identical array shape: {sourceJson}",
                "You normalize grocery ingredient names into canonical English. Return valid JSON only. Never change indexes, originalName values, array order, quantities, brands, or add items.");

            try
            {
                var content = await SendAsync(requestBody, apiKey);
                if (content == null) return null;

                var response = JsonSerializer.Deserialize<IngredientCanonicalizationResponse>(content, JsonOptions);
                if (response?.Ingredients == null || response.Ingredients.Count != source.Count ||
                    response.Ingredients.Select((item, index) => item.Index != index ||
                        !string.Equals(item.OriginalName, source[index].OriginalName, StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(item.CanonicalName)).Any(invalid => invalid))
                {
                    return null;
                }

                var canonicalNames = response.Ingredients.ToDictionary(
                    item => item.OriginalName,
                    item => NormalizeCanonicalIngredientName(item.CanonicalName),
                    StringComparer.OrdinalIgnoreCase);
                _cache.Set(cacheKey, canonicalNames, TimeSpan.FromDays(30));
                return canonicalNames;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Ingredient canonicalization failed for language {Language}", language);
                return null;
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> TranslateIngredientNamesAsync(IEnumerable<string> names, string targetLanguage)
        {
            var language = SupportedLanguages.FirstOrDefault(item => item.Equals(targetLanguage?.Trim(), StringComparison.OrdinalIgnoreCase));
            var distinctNames = (names ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctNames.Count == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (language == null || language.Equals("English", StringComparison.OrdinalIgnoreCase))
                return distinctNames.ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);

            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            var translatedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var batch in distinctNames.Chunk(75))
            {
                var source = batch.Select((name, index) => new IngredientNameTranslation
                {
                    Index = index,
                    Name = name,
                    DisplayName = name
                }).ToList();
                var sourceJson = JsonSerializer.Serialize(source);
                var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceJson)));
                var cacheKey = $"ingredient-name-translations:v2:{language.ToLowerInvariant()}:{sourceHash}";
                if (_cache.TryGetValue(cacheKey, out Dictionary<string, string> cachedBatch))
                {
                    foreach (var item in cachedBatch) translatedNames[item.Key] = item.Value;
                    continue;
                }

                var requestBody = CreateRequestBody(
                    $"Translate every displayName in this JSON array from English to {language}. Use the common concise grocery-store term. Preserve index and name exactly. Return an object with an ingredients property containing the identical array shape: {sourceJson}",
                    "You translate grocery ingredient names accurately. Return valid JSON only. Never change indexes, name values, array order, quantities, brands, or add items.");

                try
                {
                    var content = await SendAsync(requestBody, apiKey);
                    if (content == null)
                    {
                        foreach (var item in source) translatedNames[item.Name] = GetCommonIngredientTranslation(item.Name, language);
                        continue;
                    }

                    var response = JsonSerializer.Deserialize<IngredientNameTranslationResponse>(content, JsonOptions);
                    if (response?.Ingredients == null || response.Ingredients.Count != source.Count ||
                        response.Ingredients.Select((item, index) => item.Index != index ||
                            string.IsNullOrWhiteSpace(item.DisplayName)).Any(invalid => invalid))
                    {
                        _logger.LogWarning("Ingredient name translation returned an invalid batch for language {Language}", language);
                        foreach (var item in source) translatedNames[item.Name] = GetCommonIngredientTranslation(item.Name, language);
                        continue;
                    }

                    var translatedBatch = response.Ingredients
                        .Select((item, index) => new { source[index].Name, DisplayName = item.DisplayName.Trim() })
                        .ToDictionary(item => item.Name, item => item.DisplayName, StringComparer.OrdinalIgnoreCase);
                    _cache.Set(cacheKey, translatedBatch, TimeSpan.FromDays(30));
                    foreach (var item in translatedBatch) translatedNames[item.Key] = item.Value;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Ingredient name translation failed for language {Language}", language);
                    foreach (var item in source) translatedNames[item.Name] = GetCommonIngredientTranslation(item.Name, language);
                }
            }

            return translatedNames;
        }

        private static Dictionary<string, string> CreateCommonIngredientTranslations(IEnumerable<string> names, string language)
        {
            return names.ToDictionary(name => name, name => GetCommonIngredientTranslation(name, language), StringComparer.OrdinalIgnoreCase);
        }

        private static string GetCommonIngredientTranslation(string name, string language)
        {
            return CommonIngredientTranslations.TryGetValue(language, out var translations) &&
                   translations.TryGetValue(name.Trim(), out var translatedName)
                ? translatedName
                : name;
        }

        private string GetApiKey() => Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? _configuration["RecipeTranslation:OpenAIApiKey"]
            ?? _configuration["NutritionEstimation:OpenAIApiKey"]
            ?? _configuration["AIImageGeneration:OpenAIApiKey"];

        private object CreateRequestBody(string prompt, string systemMessage) => new
        {
            model = _configuration["RecipeTranslation:OpenAIModel"] ?? "gpt-4o-mini",
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = prompt }
            }
        };

        private async Task<string> SendAsync(object requestBody, string apiKey)
        {
            var endpoint = _configuration["RecipeTranslation:OpenAIEndpoint"] ?? "https://api.openai.com/v1/chat/completions";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(requestBody);
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Recipe translation failed with status {StatusCode}", (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }

        private static string NormalizeCanonicalIngredientName(string name)
        {
            var normalized = name.Trim();
            return normalized.Length == 0 ? normalized : char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }

        private static RecipeResponse CloneRecipe(RecipeResponse recipe) => new RecipeResponse
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Creator = recipe.Creator,
            Description = recipe.Description,
            Instructions = recipe.Instructions,
            Language = recipe.Language,
            Portions = recipe.Portions,
            PreparationMinutes = recipe.PreparationMinutes,
            CookingMinutes = recipe.CookingMinutes,
            ChillingMinutes = recipe.ChillingMinutes,
            CoolingMinutes = recipe.CoolingMinutes,
            RestingMinutes = recipe.RestingMinutes,
            ShelfLifeDays = recipe.ShelfLifeDays,
            CanBeFrozen = recipe.CanBeFrozen,
            Calories = recipe.Calories,
            ProteinGrams = recipe.ProteinGrams,
            CarbohydrateGrams = recipe.CarbohydrateGrams,
            FatGrams = recipe.FatGrams,
            FiberGrams = recipe.FiberGrams,
            SugarGrams = recipe.SugarGrams,
            SodiumMilligrams = recipe.SodiumMilligrams,
            MadeCount = recipe.MadeCount,
            AverageRating = recipe.AverageRating,
            RatingCount = recipe.RatingCount,
            Categories = new List<string>(recipe.Categories ?? new List<string>()),
            Tags = new List<string>(recipe.Tags ?? new List<string>()),
            Created = recipe.Created,
            Image = recipe.Image == null ? null : new ImageResponse
            {
                Id = recipe.Image.Id,
                Url = recipe.Image.Url,
                Caption = recipe.Image.Caption
            },
            Ingredients = (recipe.Ingredients ?? new List<IngredientResponse>()).Select(ingredient => new IngredientResponse
            {
                Name = ingredient.Name,
                Description = ingredient.Description,
                Language = ingredient.Language,
                Amount = ingredient.Amount,
                AmountType = ingredient.AmountType,
                Group = ingredient.Group,
                Created = ingredient.Created,
                Image = ingredient.Image
            }).ToList()
        };

        private static List<InstructionTranslationSegment> ExtractInstructionSegments(string instructions) => InstructionMarkupPattern
            .Split(instructions ?? string.Empty)
            .Where(part => part.Any(char.IsLetter) && !InstructionMarkupPattern.IsMatch(part))
            .Select((text, index) => new InstructionTranslationSegment { Index = index, Text = text })
            .ToList();

        private static string ApplyInstructionTranslation(string instructions, IReadOnlyList<InstructionTranslationSegment> translatedSegments)
        {
            var parts = InstructionMarkupPattern.Split(instructions ?? string.Empty);
            var translatedIndex = 0;
            for (var index = 0; index < parts.Length; index++)
            {
                if (!parts[index].Any(char.IsLetter) || InstructionMarkupPattern.IsMatch(parts[index])) continue;
                parts[index] = translatedSegments[translatedIndex++].Text ?? parts[index];
            }
            return string.Concat(parts);
        }

        private class TranslationPayload
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public List<InstructionTranslationSegment> InstructionSegments { get; set; } = new List<InstructionTranslationSegment>();
            public string Portions { get; set; }
            public string ImageCaption { get; set; }
            public List<IngredientTranslation> Ingredients { get; set; } = new List<IngredientTranslation>();
        }

        private class InstructionTranslationSegment
        {
            public int Index { get; set; }
            public string Text { get; set; }
        }

        private class IngredientTranslation
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string AmountType { get; set; }
            public string Group { get; set; }
        }

        private class IngredientCanonicalizationResponse
        {
            public List<IngredientCanonicalization> Ingredients { get; set; } = new List<IngredientCanonicalization>();
        }

        private class IngredientNameTranslationResponse
        {
            public List<IngredientNameTranslation> Ingredients { get; set; } = new List<IngredientNameTranslation>();
        }

        private class IngredientNameTranslation
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string DisplayName { get; set; }
        }

        private class IngredientCanonicalization
        {
            public int Index { get; set; }
            public string OriginalName { get; set; }
            public string CanonicalName { get; set; }
        }

        private class SummaryTranslationResponse
        {
            public List<SummaryTranslation> Recipes { get; set; } = new List<SummaryTranslation>();
        }

        private class SummaryTranslation
        {
            public Guid Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
        }
    }
}