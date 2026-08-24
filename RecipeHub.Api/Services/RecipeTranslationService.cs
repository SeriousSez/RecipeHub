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
using Microsoft.EntityFrameworkCore;
using RecipeHub.Infrastructure;

namespace RecipeHub.Api.Services
{
    public interface IRecipeTranslationService
    {
        Task<RecipeResponse> TranslateAsync(RecipeResponse recipe, string targetLanguage);
        Task<List<RecipeResponse>> TranslateSummariesAsync(List<RecipeResponse> recipes, string targetLanguage);
        Task<IReadOnlyDictionary<string, string>> CanonicalizeIngredientNamesAsync(IEnumerable<string> names, string sourceLanguage);
        Task<IReadOnlyDictionary<string, string>> TranslateIngredientNamesAsync(IEnumerable<string> names, string targetLanguage, IReadOnlyDictionary<string, string> contexts = null);
        Task<IReadOnlyDictionary<string, string>> GetIngredientTranslationsAsync(string ingredientName);
        Task SaveIngredientTranslationAsync(string ingredientName, string language, string translatedName);
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
        private readonly RecipeHubContext _context;

        public OpenAiRecipeTranslationService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<OpenAiRecipeTranslationService> logger, RecipeHubContext context)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _context = context;
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
                Categories = new List<string>(recipe.Categories ?? new List<string>()),
                Tags = new List<string>(recipe.Tags ?? new List<string>()),
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
                    new { role = "user", content = $"Translate every string value in this recipe JSON from English to {language}, including the Categories and Tags arrays. Keep taxonomy values concise, preserve their meaning, keep empty values empty, and return the identical JSON shape: {sourceJson}" }
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
                    translation.Categories == null || translation.Categories.Count != source.Categories.Count ||
                    translation.Tags == null || translation.Tags.Count != source.Tags.Count ||
                    translation.InstructionSegments == null || translation.InstructionSegments.Count != source.InstructionSegments.Count ||
                    translation.InstructionSegments.Select((segment, index) => segment.Index != index).Any(invalid => invalid)) return recipe;

                var translatedRecipe = CloneRecipe(recipe);
                translatedRecipe.Title = translation.Title ?? recipe.Title;
                translatedRecipe.Description = translation.Description ?? recipe.Description;
                translatedRecipe.Instructions = ApplyInstructionTranslation(recipe.Instructions, translation.InstructionSegments);
                translatedRecipe.Portions = translation.Portions ?? recipe.Portions;
                translatedRecipe.Categories = translation.Categories.Select(value => value ?? string.Empty).ToList();
                translatedRecipe.Tags = translation.Tags.Select(value => value ?? string.Empty).ToList();
                if (translatedRecipe.Image != null) translatedRecipe.Image.Caption = translation.ImageCaption ?? translatedRecipe.Image.Caption;
                translatedRecipe.Language = language;
                var savedIngredientTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < translatedRecipe.Ingredients.Count; index++)
                {
                    var translatedIngredient = translation.Ingredients[index];
                    translatedRecipe.Ingredients[index].Name = translatedIngredient.Name ?? translatedRecipe.Ingredients[index].Name;
                    translatedRecipe.Ingredients[index].Description = translatedIngredient.Description ?? translatedRecipe.Ingredients[index].Description;
                    translatedRecipe.Ingredients[index].AmountType = translatedIngredient.AmountType ?? translatedRecipe.Ingredients[index].AmountType;
                    translatedRecipe.Ingredients[index].Group = translatedIngredient.Group ?? translatedRecipe.Ingredients[index].Group;
                    translatedRecipe.Ingredients[index].Language = language;
                    if (!string.IsNullOrWhiteSpace(recipe.Ingredients[index].Name) && !string.IsNullOrWhiteSpace(translatedIngredient.Name))
                    {
                        savedIngredientTranslations[recipe.Ingredients[index].Name] = translatedIngredient.Name;
                    }
                }

                try
                {
                    await SaveIngredientTranslationsAsync(savedIngredientTranslations, language);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Ingredient translations could not be persisted for recipe {RecipeId}", recipe.Id);
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

        public async Task<IReadOnlyDictionary<string, string>> TranslateIngredientNamesAsync(IEnumerable<string> names, string targetLanguage, IReadOnlyDictionary<string, string> contexts = null)
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

            var savedTranslations = await GetSavedIngredientTranslationsAsync(distinctNames, language);
            var missingNames = distinctNames.Where(name => !savedTranslations.ContainsKey(name)).ToList();
            if (missingNames.Count == 0) return savedTranslations;

            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return savedTranslations.Count > 0 ? savedTranslations : null;

            var translatedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var batch in missingNames.Chunk(75))
            {
                var source = batch.Select((name, index) => new IngredientNameTranslation
                {
                    Index = index,
                    Name = name,
                    DisplayName = name,
                    Context = contexts != null && contexts.TryGetValue(name, out var context) ? context : null
                }).ToList();
                var sourceJson = JsonSerializer.Serialize(source);
                var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceJson)));
                var cacheKey = $"ingredient-name-translations:v3:{language.ToLowerInvariant()}:{sourceHash}";
                if (_cache.TryGetValue(cacheKey, out Dictionary<string, string> cachedBatch))
                {
                    await SaveIngredientTranslationsAsync(cachedBatch, language);
                    foreach (var item in cachedBatch) translatedNames[item.Key] = item.Value;
                    continue;
                }

                var requestBody = CreateRequestBody(
                    $"Translate every displayName in this JSON array from English to {language} as an edible culinary ingredient search term. Use the context field to resolve ambiguity. Prefer the food meaning over brands, companies, product names, or proper nouns; for example, translate apple as the fruit, æble in Danish, even if Apple could be a brand. Return a concise grocery ingredient term suitable for a supermarket search. Preserve index, name, and context exactly. Return an object with an ingredients property containing the identical array shape: {sourceJson}",
                    "You translate recipe ingredients for grocery searches. Interpret names as edible ingredients unless the context clearly says otherwise. Never return brand or company names as ingredient translations. Return valid JSON only. Never change indexes, name values, context values, array order, quantities, or add items.");

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
                    await SaveIngredientTranslationsAsync(translatedBatch, language);
                    foreach (var item in translatedBatch) translatedNames[item.Key] = item.Value;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Ingredient name translation failed for language {Language}", language);
                    foreach (var item in source) translatedNames[item.Name] = GetCommonIngredientTranslation(item.Name, language);
                }
            }

            foreach (var saved in savedTranslations) translatedNames[saved.Key] = saved.Value;
            return translatedNames;
        }

        private async Task<Dictionary<string, string>> GetSavedIngredientTranslationsAsync(IEnumerable<string> names, string language)
        {
            var normalizedNames = names.Select(name => name.Trim()).ToList();
            var translations = await _context.IngredientTranslations
                .AsNoTracking()
                .Where(translation => translation.Language == language && normalizedNames.Contains(translation.IngredientName))
                .Select(translation => new { translation.IngredientName, translation.TranslatedName })
                .ToListAsync();
            return translations.ToDictionary(item => item.IngredientName, item => item.TranslatedName, StringComparer.OrdinalIgnoreCase);
        }

        private async Task SaveIngredientTranslationsAsync(IReadOnlyDictionary<string, string> translations, string language)
        {
            var names = translations.Keys.Select(name => name.Trim()).ToList();
            var existing = await _context.IngredientTranslations
                .Where(translation => names.Contains(translation.IngredientName) && translation.Language == language)
                .ToListAsync();
            foreach (var translation in translations)
            {
                var saved = existing.FirstOrDefault(item => item.IngredientName.Equals(translation.Key, StringComparison.OrdinalIgnoreCase));
                if (saved == null)
                {
                    _context.IngredientTranslations.Add(new Domain.Entities.Recipe.IngredientTranslation
                    {
                        Id = Guid.NewGuid(),
                        IngredientName = translation.Key.Trim(),
                        Language = language,
                        TranslatedName = translation.Value,
                        Source = "OpenAI"
                    });
                }
                else
                {
                    saved.TranslatedName = translation.Value;
                    saved.Source = "OpenAI";
                }
            }
            await _context.SaveChangesAsync();
        }

        public Task SaveIngredientTranslationAsync(string ingredientName, string language, string translatedName)
        {
            return SaveIngredientTranslationsAsync(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ingredientName] = translatedName },
                language);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetIngredientTranslationsAsync(string ingredientName)
        {
            var normalizedName = ingredientName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName)) return new Dictionary<string, string>();

            return await _context.IngredientTranslations
            .AsNoTracking()
            .Where(translation => translation.IngredientName == normalizedName)
            .ToDictionaryAsync(translation => translation.Language, translation => translation.TranslatedName);
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
                parts[index] = PreserveBoundaryWhitespace(parts[index], translatedSegments[translatedIndex++].Text);
            }
            return string.Concat(parts);
        }

        private static string PreserveBoundaryWhitespace(string original, string translated)
        {
            if (translated == null) return original;

            var leadingLength = original.TakeWhile(char.IsWhiteSpace).Count();
            var trailingLength = original.Reverse().TakeWhile(char.IsWhiteSpace).Count();
            var leading = original.Substring(0, leadingLength);
            var trailing = trailingLength == 0 ? string.Empty : original.Substring(original.Length - trailingLength);
            var contentEnd = translated.Length;
            while (contentEnd > 0 && char.IsWhiteSpace(translated[contentEnd - 1])) contentEnd--;
            var contentStart = 0;
            while (contentStart < contentEnd && char.IsWhiteSpace(translated[contentStart])) contentStart++;

            return leading + translated.Substring(contentStart, contentEnd - contentStart) + trailing;
        }

        private class TranslationPayload
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public List<InstructionTranslationSegment> InstructionSegments { get; set; } = new List<InstructionTranslationSegment>();
            public string Portions { get; set; }
            public string ImageCaption { get; set; }
            public List<string> Categories { get; set; } = new List<string>();
            public List<string> Tags { get; set; } = new List<string>();
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
            public string Context { get; set; }
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