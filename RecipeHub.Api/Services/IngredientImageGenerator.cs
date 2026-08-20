using Microsoft.Extensions.Configuration;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using System;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public class IngredientImageGenerator : IIngredientImageGenerator
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAiIngredientImageGenerator _openAiGenerator;
        private readonly LocalStableDiffusionIngredientImageGenerator _stableDiffusionGenerator;
        private readonly WikipediaIngredientImageGenerator _wikipediaGenerator;

        public IngredientImageGenerator(
            IConfiguration configuration,
            OpenAiIngredientImageGenerator openAiGenerator,
            LocalStableDiffusionIngredientImageGenerator stableDiffusionGenerator,
            WikipediaIngredientImageGenerator wikipediaGenerator)
        {
            _configuration = configuration;
            _openAiGenerator = openAiGenerator;
            _stableDiffusionGenerator = stableDiffusionGenerator;
            _wikipediaGenerator = wikipediaGenerator;
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

            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                var openAi = await _openAiGenerator.GenerateAsync(ingredientName, description);
                if (openAi != null)
                {
                    return openAi;
                }

                return await _wikipediaGenerator.GenerateAsync(ingredientName, description);
            }

            if (provider.Equals("StableDiffusion", StringComparison.OrdinalIgnoreCase))
            {
                var styled = await _stableDiffusionGenerator.GenerateAsync(ingredientName, description);
                if (styled != null)
                {
                    return styled;
                }
            }

            return await _wikipediaGenerator.GenerateAsync(ingredientName, description);
        }
    }
}