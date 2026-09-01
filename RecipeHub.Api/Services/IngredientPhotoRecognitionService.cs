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
    public interface IIngredientPhotoRecognitionService
    {
        Task<IngredientPhotoRecognitionResponse> RecognizeAsync(IngredientPhotoRecognitionRequest request);
    }

    public class OpenAiIngredientPhotoRecognitionService : IIngredientPhotoRecognitionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAiIngredientPhotoRecognitionService> _logger;

        public OpenAiIngredientPhotoRecognitionService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiIngredientPhotoRecognitionService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IngredientPhotoRecognitionResponse> RecognizeAsync(IngredientPhotoRecognitionRequest request)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? _configuration["IngredientPhotoRecognition:OpenAIApiKey"]
                ?? _configuration["NutritionEstimation:OpenAIApiKey"]
                ?? _configuration["AIImageGeneration:OpenAIApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Ingredient photo recognition is configured, but no OpenAI API key is available");
                return new IngredientPhotoRecognitionResponse { ErrorCode = "not_configured" };
            }

            var images = (request.Images ?? new List<IngredientPhotoImage>())
                .Where(image => !string.IsNullOrWhiteSpace(image?.ImageBase64))
                .ToList();
            if (images.Count == 0)
            {
                return new IngredientPhotoRecognitionResponse { ErrorCode = "no_images" };
            }

            var endpoint = _configuration["IngredientPhotoRecognition:OpenAIEndpoint"] ?? "https://api.openai.com/v1/chat/completions";
            var model = _configuration["IngredientPhotoRecognition:OpenAIModel"] ?? "gpt-4o-mini";
            var language = string.IsNullOrWhiteSpace(request.Language) ? "English" : request.Language;
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var prompt = $@"These {images.Count} photo(s) show ingredients in someone's pantry. They may be different photos of the same items (for example one photo of the product and another of its expiry date or quantity label), so combine information across all photos for the same item where it makes sense.
Identify the distinct, whole edible food ingredients visible (for example individual fruits, vegetables, packaged goods, dairy, meat, or pantry staples). Ignore utensils, containers, hands, and non-food items.
For each ingredient, include, if visible on any of the photos:
- name: a concise, singular grocery-style name in {language} (for example ""tomato"", not ""two red tomatoes"").
- amount: a numeric quantity if a count or weight/volume is legible, otherwise null.
- amountType: one of exactly these units: Piece, Gram, Kilogram, Milliliter, Liter, Teaspoon, Tablespoon, Cup, Ounce, Pound. Use Piece when counting whole items and no other unit is shown.
- expirationDate: an ISO date (YYYY-MM-DD) if a best-before or expiry date is legible on any photo, otherwise null. Today's date is {today}; only return dates that are printed or clearly legible, never guess.
Do not include duplicates of the same item, and do not fabricate values you cannot actually see.
Return only a JSON object with an ""items"" array of objects with fields name, amount, amountType, expirationDate. If no food ingredients are visible, return an empty array.";

            var content = new List<object> { new { type = "text", text = prompt } };
            content.AddRange(images.Select(image => (object)new
            {
                type = "image_url",
                image_url = new { url = $"data:{(string.IsNullOrWhiteSpace(image.ContentType) ? "image/jpeg" : image.ContentType)};base64,{image.ImageBase64}" }
            }));

            var requestBody = new
            {
                model,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = "You identify edible food ingredients, and their visible quantity and expiry date, from photos for a grocery and pantry app. Return valid JSON only." },
                    new { role = "user", content }
                }
            };

            try
            {
                using var apiResponse = await SendOpenAiRequestAsync(endpoint, apiKey, requestBody);
                var responseBody = await apiResponse.Content.ReadAsStringAsync();
                if (!apiResponse.IsSuccessStatusCode)
                {
                    var errorCode = GetOpenAiErrorCode(responseBody);
                    _logger.LogWarning("OpenAI ingredient photo recognition failed with status {StatusCode} and code {ErrorCode}: {ResponseBody}", (int)apiResponse.StatusCode, errorCode, responseBody);
                    return new IngredientPhotoRecognitionResponse { Provider = "OpenAI", ErrorCode = errorCode };
                }

                using var responseDocument = JsonDocument.Parse(responseBody);
                var messageContent = responseDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                using var parsedContent = JsonDocument.Parse(messageContent ?? "{}");
                var items = parsedContent.RootElement.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
                    ? itemsElement.EnumerateArray()
                        .Select(ParseRecognizedItem)
                        .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name))
                        .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList()
                    : new List<RecognizedPantryItem>();

                return new IngredientPhotoRecognitionResponse { Items = items, Provider = "OpenAI" };
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "OpenAI ingredient photo recognition failed");
                return new IngredientPhotoRecognitionResponse { Provider = "OpenAI", ErrorCode = "recognition_failed" };
            }
        }

        private static readonly string[] AllowedAmountTypes = { "Piece", "Gram", "Kilogram", "Milliliter", "Liter", "Teaspoon", "Tablespoon", "Cup", "Ounce", "Pound" };

        private static RecognizedPantryItem ParseRecognizedItem(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            var name = element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(name)) return null;

            decimal? amount = element.TryGetProperty("amount", out var amountElement) && amountElement.ValueKind == JsonValueKind.Number && amountElement.GetDecimal() > 0
                ? amountElement.GetDecimal()
                : null;

            var amountType = element.TryGetProperty("amountType", out var amountTypeElement) && amountTypeElement.ValueKind == JsonValueKind.String
                ? AllowedAmountTypes.FirstOrDefault(unit => unit.Equals(amountTypeElement.GetString(), StringComparison.OrdinalIgnoreCase))
                : null;

            var expirationDate = element.TryGetProperty("expirationDate", out var expirationElement) && expirationElement.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(expirationElement.GetString(), out var parsedDate)
                ? parsedDate.ToString("yyyy-MM-dd")
                : null;

            return new RecognizedPantryItem { Name = name, Amount = amount, AmountType = amountType ?? "Piece", ExpirationDate = expirationDate };
        }

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
