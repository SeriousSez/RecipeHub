using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class LocalStableDiffusionIngredientImageGenerator : IIngredientImageGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LocalStableDiffusionIngredientImageGenerator> _logger;

        public LocalStableDiffusionIngredientImageGenerator(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<LocalStableDiffusionIngredientImageGenerator> logger)
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
            if (!provider.Equals("StableDiffusion", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var endpoint = section["StableDiffusionEndpoint"] ?? "http://127.0.0.1:7860";
            var stylePath = section["StyleReferenceImagePath"] ?? "..\\RecipeHub\\src\\assets\\images\\milk.jpg";
            var resolvedPath = ResolveStylePath(stylePath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || File.Exists(resolvedPath) == false)
            {
                _logger.LogWarning("StableDiffusion style reference image not found at {StylePath}", stylePath);
                return null;
            }

            var safeIngredientName = string.IsNullOrWhiteSpace(ingredientName) ? "ingredient" : ingredientName.Trim();
            var safeDescription = string.IsNullOrWhiteSpace(description) ? "" : description.Trim();

            var styleImageBytes = await File.ReadAllBytesAsync(resolvedPath);
            var initImage = Convert.ToBase64String(styleImageBytes);

            var payload = new
            {
                prompt = $"single {safeIngredientName}, same pen-and-ink sketch carton style as reference, monochrome navy ink lines, clean light background, no text. {safeDescription}",
                negative_prompt = "text, words, letters, watermark, logo, blurry, low quality",
                init_images = new[] { initImage },
                denoising_strength = 0.7,
                sampler_name = "Euler a",
                steps = 28,
                cfg_scale = 7,
                width = 512,
                height = 512
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint.TrimEnd('/')}/sdapi/v1/img2img");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "StableDiffusion endpoint not reachable at {Endpoint}", endpoint);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("StableDiffusion generation failed for {Ingredient}. Status: {StatusCode}", safeIngredientName, (int)response.StatusCode);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            if (!json.RootElement.TryGetProperty("images", out var images) || images.GetArrayLength() == 0)
            {
                return null;
            }

            var b64 = images[0].GetString();
            if (string.IsNullOrWhiteSpace(b64))
            {
                return null;
            }

            return new ImageViewModel
            {
                Url = $"data:image/png;base64,{b64}",
                Caption = $"AI generated (style matched) image for {safeIngredientName}"
            };
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

            var fromAppBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
            return fromAppBase;
        }
    }
}