using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public interface IRecipeGenerationService
    {
        Task<GeneratedRecipeResponse> GenerateAsync(RecipeGenerationRequest request);
    }

    public class OpenAiRecipeGenerationService : IRecipeGenerationService
    {
        private static readonly string[] AllowedAmountTypes = { "Piece", "Gram", "Kilogram", "Milliliter", "Liter", "Teaspoon", "Tablespoon", "Cup", "Ounce", "Pound", "Pinch or dash", "Clove", "To taste" };

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAiRecipeGenerationService> _logger;

        public OpenAiRecipeGenerationService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiRecipeGenerationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GeneratedRecipeResponse> GenerateAsync(RecipeGenerationRequest request)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? _configuration["RecipeGeneration:OpenAIApiKey"]
                ?? _configuration["NutritionEstimation:OpenAIApiKey"]
                ?? _configuration["AIImageGeneration:OpenAIApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Recipe generation is configured, but no OpenAI API key is available");
                return new GeneratedRecipeResponse { ErrorCode = "not_configured" };
            }

            var prompt = string.IsNullOrWhiteSpace(request?.Prompt) ? null : request.Prompt.Trim();
            var pantryItems = (request?.PantryItems ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
            if (prompt == null && pantryItems.Count == 0)
            {
                return new GeneratedRecipeResponse { ErrorCode = "no_input" };
            }

            var endpoint = _configuration["RecipeGeneration:OpenAIEndpoint"] ?? "https://api.openai.com/v1/chat/completions";
            var model = _configuration["RecipeGeneration:OpenAIModel"] ?? "gpt-4o-mini";
            var language = string.IsNullOrWhiteSpace(request?.Language) ? "English" : request.Language;
            var portions = string.IsNullOrWhiteSpace(request?.Portions) ? "a reasonable number of" : request.Portions;
            var allowedAmountTypesJson = JsonSerializer.Serialize(AllowedAmountTypes);

            var instructionParts = new List<string>();
            if (pantryItems.Count > 0) instructionParts.Add($"Use mainly these available ingredients where sensible, and add a few common pantry staples (salt, oil, water, etc.) if needed: {string.Join(", ", pantryItems)}.");
            if (prompt != null) instructionParts.Add($"Additional request from the user: {prompt}");

            var generationPrompt = $@"Create one complete, realistic, home-cookable recipe in {language}, for {portions} servings.
{string.Join(" ", instructionParts)}
Write clear step-by-step instructions as an HTML string using <p> tags per step (no markdown).
For each ingredient, choose amountType from exactly this list: {allowedAmountTypesJson}. Use realistic amounts. Group ingredients under short group names when it helps (e.g. ""Sauce"", ""Topping""), or leave group empty for a single list.
Estimate nutrition per serving using typical values for the ingredients and amounts.
Suggest 1-3 short lowercase category words (e.g. ""dinner"", ""dessert"") and 0-4 short lowercase descriptive tags (e.g. ""vegetarian"", ""quick"", ""spicy"").
Return only a JSON object with fields: title, description, instructions, portions (string), preparationMinutes, cookingMinutes, proofingMinutes, chillingMinutes, coolingMinutes, restingMinutes, shelfLifeDays, canBeFrozen (all nullable numbers/booleans, null if not applicable), calories, proteinGrams, carbohydrateGrams, fatGrams, fiberGrams, sugarGrams, sodiumMilligrams (nullable numbers, per serving), categories (array of strings), tags (array of strings), and ingredients (array of objects with name, description, amount, amountType, group).";

            var requestBody = new
            {
                model,
                temperature = 0.7,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = "You are a creative but practical home-cooking recipe writer. Return valid JSON only." },
                    new { role = "user", content = generationPrompt }
                }
            };

            try
            {
                using var apiResponse = await SendOpenAiRequestAsync(endpoint, apiKey, requestBody);
                var responseBody = await apiResponse.Content.ReadAsStringAsync();
                if (!apiResponse.IsSuccessStatusCode)
                {
                    var errorCode = GetOpenAiErrorCode(responseBody);
                    _logger.LogWarning("OpenAI recipe generation failed with status {StatusCode} and code {ErrorCode}", (int)apiResponse.StatusCode, errorCode);
                    return new GeneratedRecipeResponse { Provider = "OpenAI", ErrorCode = errorCode };
                }

                using var responseDocument = JsonDocument.Parse(responseBody);
                var content = responseDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                using var parsedContent = JsonDocument.Parse(content ?? "{}");
                var root = parsedContent.RootElement;

                var ingredients = root.TryGetProperty("ingredients", out var ingredientsElement) && ingredientsElement.ValueKind == JsonValueKind.Array
                    ? ingredientsElement.EnumerateArray().Select(ParseIngredient).Where(ingredient => ingredient != null && !string.IsNullOrWhiteSpace(ingredient.Name)).ToList()
                    : new List<GeneratedRecipeIngredient>();

                var groupOrders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var ingredientOrders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var ingredient in ingredients)
                {
                    var groupKey = ingredient.Group ?? string.Empty;
                    if (!groupOrders.ContainsKey(groupKey)) groupOrders[groupKey] = groupOrders.Count;
                    ingredient.GroupOrder = groupOrders[groupKey];
                    ingredient.IngredientOrder = ingredientOrders.TryGetValue(groupKey, out var order) ? order : 0;
                    ingredientOrders[groupKey] = ingredient.IngredientOrder + 1;
                }

                return new GeneratedRecipeResponse
                {
                    Title = GetString(root, "title"),
                    Description = GetString(root, "description"),
                    Instructions = GetString(root, "instructions"),
                    Portions = GetString(root, "portions"),
                    PreparationMinutes = GetInt(root, "preparationMinutes"),
                    CookingMinutes = GetInt(root, "cookingMinutes"),
                    ProofingMinutes = GetInt(root, "proofingMinutes"),
                    ChillingMinutes = GetInt(root, "chillingMinutes"),
                    CoolingMinutes = GetInt(root, "coolingMinutes"),
                    RestingMinutes = GetInt(root, "restingMinutes"),
                    ShelfLifeDays = GetInt(root, "shelfLifeDays"),
                    CanBeFrozen = GetBool(root, "canBeFrozen"),
                    Calories = GetDecimal(root, "calories"),
                    ProteinGrams = GetDecimal(root, "proteinGrams"),
                    CarbohydrateGrams = GetDecimal(root, "carbohydrateGrams"),
                    FatGrams = GetDecimal(root, "fatGrams"),
                    FiberGrams = GetDecimal(root, "fiberGrams"),
                    SugarGrams = GetDecimal(root, "sugarGrams"),
                    SodiumMilligrams = GetDecimal(root, "sodiumMilligrams"),
                    Categories = GetStringArray(root, "categories"),
                    Tags = GetStringArray(root, "tags"),
                    Ingredients = ingredients,
                    Provider = "OpenAI"
                };
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "OpenAI recipe generation failed");
                return new GeneratedRecipeResponse { Provider = "OpenAI", ErrorCode = "generation_failed" };
            }
        }

        private static GeneratedRecipeIngredient ParseIngredient(JsonElement element)
        {
            var amountType = GetString(element, "amountType");
            return new GeneratedRecipeIngredient
            {
                Name = GetString(element, "name"),
                Description = GetString(element, "description") ?? string.Empty,
                Amount = GetDecimal(element, "amount") ?? 0,
                AmountType = AllowedAmountTypes.FirstOrDefault(allowed => string.Equals(allowed, amountType, StringComparison.OrdinalIgnoreCase)) ?? "Piece",
                Group = GetString(element, "group") ?? string.Empty
            };
        }

        private static string GetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static int? GetInt(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

        private static decimal? GetDecimal(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

        private static bool? GetBool(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) ? value.GetBoolean() : null;

        private static List<string> GetStringArray(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
                : new List<string>();

        private async Task<HttpResponseMessage> SendOpenAiRequestAsync(string endpoint, string apiKey, object requestBody)
        {
            var response = await SendOpenAiRequestOnceAsync(endpoint, apiKey, requestBody);
            if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;

            var responseBody = await response.Content.ReadAsStringAsync();
            var errorCode = GetOpenAiErrorCode(responseBody);
            if (errorCode.Equals("insufficient_quota", StringComparison.OrdinalIgnoreCase)) return response;

            var retryDelay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
            retryDelay = retryDelay > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : retryDelay;
            response.Dispose();
            await Task.Delay(retryDelay);
            return await SendOpenAiRequestOnceAsync(endpoint, apiKey, requestBody);
        }

        private async Task<HttpResponseMessage> SendOpenAiRequestOnceAsync(string endpoint, string apiKey, object requestBody)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            message.Content = JsonContent.Create(requestBody);
            return await _httpClient.SendAsync(message);
        }

        private static string GetOpenAiErrorCode(string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var error = document.RootElement.GetProperty("error");
                if (error.TryGetProperty("code", out var code) && !string.IsNullOrWhiteSpace(code.GetString())) return code.GetString();
                if (error.TryGetProperty("type", out var type) && !string.IsNullOrWhiteSpace(type.GetString())) return type.GetString();
            }
            catch (JsonException)
            {
            }

            return "rate_limit_exceeded";
        }
    }
}
