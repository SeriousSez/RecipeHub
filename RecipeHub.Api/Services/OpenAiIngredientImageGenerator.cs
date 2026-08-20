using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class OpenAiIngredientImageGenerator : IIngredientImageGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAiIngredientImageGenerator> _logger;

        public OpenAiIngredientImageGenerator(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenAiIngredientImageGenerator> logger)
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

            var provider = section["Provider"] ?? "Wikipedia";
            if (!provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var apiKey = section["OpenAIApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("OpenAI image generation is selected, but no API key is configured.");
                return null;
            }

            var endpoint = section["OpenAIEndpoint"] ?? "https://api.openai.com/v1/images/edits";
            var generationEndpoint = section["OpenAIGenerationEndpoint"] ?? "https://api.openai.com/v1/images/generations";
            var model = section["OpenAIModel"] ?? "gpt-image-1";
            var size = section["OpenAIImageSize"] ?? "1024x1024";
            var useEdits = bool.TryParse(section["OpenAIUseEdits"], out var parsedUseEdits) && parsedUseEdits;
            var stylePrompt = section["OpenAIStylePrompt"] ?? "hand-drawn cross-hatching ink sketch, vintage packaging drawing, monochrome pen art, minimal color, clean white background";
            var stylePath = section["StyleReferenceImagePath"] ?? "..\\RecipeHub\\src\\assets\\images\\milk.jpg";
            var resolvedPath = ResolveStylePath(stylePath);
            var hasStyleImage = !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath);

            var safeIngredientName = string.IsNullOrWhiteSpace(ingredientName) ? "ingredient" : ingredientName.Trim();
            var safeDescription = string.IsNullOrWhiteSpace(description) ? "" : description.Trim();
            var prompt = $"A single {safeIngredientName} illustrated in the exact same style as the reference image. {stylePrompt}. No text, no labels, centered subject, clean background. {safeDescription}";

            JsonDocument json = null;
            string lastErrorBody = null;
            int? lastStatusCode = null;

            if (useEdits && hasStyleImage)
            {
                var editAttempts = new List<(string ImageFieldName, bool IncludeResponseFormat, bool IncludeSize)>
                {
                    ("image[]", false, false),
                    ("image", false, false),
                    ("image[]", true, false),
                    ("image", true, false),
                    ("image[]", false, true)
                };

                foreach (var attempt in editAttempts)
                {
                    var attemptResult = await TrySendEditRequest(endpoint, apiKey, model, prompt, size, resolvedPath, attempt.ImageFieldName, attempt.IncludeResponseFormat, attempt.IncludeSize);
                    if (attemptResult.Json != null)
                    {
                        json = attemptResult.Json;
                        break;
                    }

                    lastStatusCode = attemptResult.StatusCode;
                    lastErrorBody = attemptResult.ErrorBody;
                }
            }
            else if (useEdits && !hasStyleImage)
            {
                _logger.LogWarning("OpenAIUseEdits is enabled but style reference image was not found at {StylePath}. Falling back to generations endpoint.", stylePath);
            }
            else
            {
                _logger.LogInformation("OpenAIUseEdits is disabled. Using generations endpoint for ingredient image generation.");
            }

            if (json == null)
            {
                if (useEdits)
                {
                    _logger.LogWarning("OpenAI edit request failed for ingredient {IngredientName}. Status: {StatusCode}. Error: {ErrorBody}. Falling back to generations endpoint.", safeIngredientName, lastStatusCode, lastErrorBody);
                }

                var generationAttempts = BuildGenerationAttempts(section, model, size).ToList();
                if (generationAttempts.Count == 0)
                {
                    generationAttempts.Add((Model: "gpt-image-1", Size: "1024x1024", IncludeResponseFormat: false));
                }

                _logger.LogInformation("OpenAI generation attempts for ingredient {IngredientName}: {AttemptCount}", safeIngredientName, generationAttempts.Count);

                foreach (var generationAttempt in generationAttempts)
                {
                    _logger.LogInformation(
                        "OpenAI generation request -> Endpoint: {Endpoint}, Model: {Model}, Size: {Size}, IncludeResponseFormat: {IncludeResponseFormat}",
                        generationEndpoint,
                        generationAttempt.Model,
                        generationAttempt.Size,
                        generationAttempt.IncludeResponseFormat);

                    var generationResult = await TrySendGenerationRequest(
                        generationEndpoint,
                        apiKey,
                        generationAttempt.Model,
                        prompt,
                        generationAttempt.Size,
                        generationAttempt.IncludeResponseFormat);

                    if (generationResult.Json != null)
                    {
                        json = generationResult.Json;
                        break;
                    }

                    lastStatusCode = generationResult.StatusCode;
                    lastErrorBody = generationResult.ErrorBody;

                    _logger.LogWarning(
                        "OpenAI generation request failed for ingredient {IngredientName}. Status: {StatusCode}. Error: {ErrorBody}",
                        safeIngredientName,
                        lastStatusCode,
                        lastErrorBody);
                }

                if (json == null)
                {
                    _logger.LogWarning("OpenAI generation fallback failed for ingredient {IngredientName}. Status: {StatusCode}. Error: {ErrorBody}", safeIngredientName, lastStatusCode, lastErrorBody);
                    return null;
                }
            }

            if (!json.RootElement.TryGetProperty("data", out var dataNode) || dataNode.ValueKind != JsonValueKind.Array || dataNode.GetArrayLength() == 0)
            {
                json.Dispose();
                return null;
            }

            var first = dataNode[0];
            if (first.TryGetProperty("b64_json", out var b64Node))
            {
                var b64 = b64Node.GetString();
                if (!string.IsNullOrWhiteSpace(b64))
                {
                    json.Dispose();
                    return new ImageViewModel
                    {
                        Url = $"data:image/png;base64,{b64}",
                        Caption = $"AI generated (OpenAI style matched) image for {safeIngredientName}"
                    };
                }
            }

            if (first.TryGetProperty("url", out var urlNode))
            {
                var url = urlNode.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    json.Dispose();
                    return new ImageViewModel
                    {
                        Url = url,
                        Caption = $"AI generated (OpenAI style matched) image for {safeIngredientName}"
                    };
                }
            }

            json.Dispose();
            return null;
        }

        private async Task<(JsonDocument Json, int? StatusCode, string ErrorBody)> TrySendEditRequest(
            string endpoint,
            string apiKey,
            string model,
            string prompt,
            string size,
            string resolvedPath,
            string imageFieldName,
            bool includeResponseFormat,
            bool includeSize)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(model), "model");
            form.Add(new StringContent(prompt), "prompt");
            if (includeSize)
            {
                form.Add(new StringContent(size), "size");
            }
            if (includeResponseFormat)
            {
                form.Add(new StringContent("b64_json"), "response_format");
            }

            using var styleStream = File.OpenRead(resolvedPath);
            using var styleContent = new StreamContent(styleStream);
            styleContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentTypeFromFileExtension(resolvedPath));
            form.Add(styleContent, imageFieldName, Path.GetFileName(resolvedPath));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = form;

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI image generation request failed before receiving response.");
                return (null, null, ex.Message);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                response.Dispose();
                return (null, (int)response.StatusCode, errorBody);
            }

            var responseStream = await response.Content.ReadAsStreamAsync();
            var json = await JsonDocument.ParseAsync(responseStream);
            response.Dispose();
            return (json, (int)response.StatusCode, null);
        }

        private async Task<(JsonDocument Json, int? StatusCode, string ErrorBody)> TrySendGenerationRequest(
            string endpoint,
            string apiKey,
            string model,
            string prompt,
            string size,
            bool includeResponseFormat)
        {
            var payload = new Dictionary<string, object>
            {
                ["model"] = model,
                ["prompt"] = prompt,
                ["size"] = size,
                ["n"] = 1
            };

            if (includeResponseFormat)
            {
                payload["response_format"] = "b64_json";
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI image generation fallback request failed before receiving response.");
                return (null, null, ex.Message);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                response.Dispose();
                return (null, (int)response.StatusCode, errorBody);
            }

            var responseStream = await response.Content.ReadAsStreamAsync();
            var json = await JsonDocument.ParseAsync(responseStream);
            response.Dispose();
            return (json, (int)response.StatusCode, null);
        }

        private static IEnumerable<(string Model, string Size, bool IncludeResponseFormat)> BuildGenerationAttempts(IConfigurationSection section, string configuredModel, string configuredSize)
        {
            var fallbackModels = (section["OpenAIFallbackModels"] ?? "dall-e-2")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(model => model.Trim())
                .Where(model => !string.IsNullOrWhiteSpace(model));

            var models = new List<string> { configuredModel };
            models.AddRange(fallbackModels);

            var sizes = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredSize))
            {
                sizes.Add(configuredSize);
            }

            sizes.Add("1024x1024");
            sizes.Add("512x512");

            return models
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(model => sizes.Distinct(StringComparer.OrdinalIgnoreCase)
                    .SelectMany(size => new[]
                    {
                        (Model: model, Size: size, IncludeResponseFormat: false),
                        (Model: model, Size: size, IncludeResponseFormat: true)
                    }));
        }

        private static string ResolveStylePath(string configuredPath)
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var fromCurrentDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
            if (File.Exists(fromCurrentDirectory))
            {
                return fromCurrentDirectory;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        }

        private static string GetContentTypeFromFileExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }
    }
}
