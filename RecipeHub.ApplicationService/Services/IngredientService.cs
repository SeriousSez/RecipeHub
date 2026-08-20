using AutoMapper;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using RecipeHub.Infrastructure.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using RecipeHub.Domain.Responses;
using System;
using System.Linq;

namespace RecipeHub.ApplicationService.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly ILogger<IngredientService> _logger;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IImageRepository _imageRepository;
        private readonly IIngredientImageGenerator _ingredientImageGenerator;
        private readonly IMapper _mapper;

        public IngredientService(
            ILogger<IngredientService> logger,
            IIngredientRepository ingredientRepository,
            IImageRepository imageRepository,
            IIngredientImageGenerator ingredientImageGenerator,
            IMapper mapper)
        {
            _logger = logger;
            _ingredientRepository = ingredientRepository;
            _imageRepository = imageRepository;
            _ingredientImageGenerator = ingredientImageGenerator;
            _mapper = mapper;
        }

        public async Task<Ingredient> Create(IngredientViewModel model)
        {
            if (model.Image == null || string.IsNullOrWhiteSpace(model.Image.Url) || IsPlaceholderImageUrl(model.Image.Url))
            {
                model.Image = null;
                var generatedImage = await _ingredientImageGenerator.GenerateAsync(model.Name, model.Description);
                if (generatedImage != null)
                {
                    model.Image = generatedImage;
                }
            }

            var ingredient = _mapper.Map<Ingredient>(model);
            await _ingredientRepository.Create(ingredient);

            _logger.LogTrace("Ingredient created! Ingredient: {@Ingredient}", ingredient);

            return ingredient;
        }

        public async Task<Ingredient> Get(IngredientResponse model)
        {
            var ingredient = await _ingredientRepository.GetByName(model.Name);
            await _ingredientRepository.Create(ingredient);

            _logger.LogTrace("Ingredient created! Ingredient: {@Ingredient}", ingredient);

            return ingredient;
        }

        public async Task<IEnumerable<IngredientResponse>> GetAll()
        {
            var ingredients = await _ingredientRepository.GetAllFull();

            var ingredientList = new List<IngredientResponse>();
            foreach (var ingredient in ingredients)
            {
                var ingredientResponse = _mapper.Map<IngredientResponse>(ingredient);
                ingredientList.Add(ingredientResponse);
            }

            return ingredientList;
        }

        public async Task<IEnumerable<IngredientResponse>> GetAllLite()
        {
            var ingredients = await _ingredientRepository.GetAllLite();

            var ingredientList = new List<IngredientResponse>();
            foreach (var ingredient in ingredients)
            {
                var ingredientResponse = _mapper.Map<IngredientResponse>(ingredient);
                ingredientResponse.Image = null;
                ingredientList.Add(ingredientResponse);
            }

            return ingredientList;
        }

        public async Task<IngredientResponse> GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var ingredient = await _ingredientRepository.GetByNameFull(name);
            if (ingredient == null && !string.Equals(name, name.Trim(), StringComparison.Ordinal))
            {
                ingredient = await _ingredientRepository.GetByNameFull(name.Trim());
            }

            if (ingredient == null)
                return null;

            return _mapper.Map<IngredientResponse>(ingredient);
        }

        public async Task<Ingredient> Update(IngredientResponse model)
        {
            var ingredient = await _ingredientRepository.GetByName(model.Name);
            ingredient.Name = model.Name;
            ingredient.Description = model.Description;

            var image = await _imageRepository.GetByUrl(model.Image.Url);
            if (image == null)
            {
                await _imageRepository.Delete(ingredient.Image);
                var newImage = _mapper.Map<Image>(model);
                await _imageRepository.Create(newImage);
            }

            await _ingredientRepository.Update(ingredient);

            _logger.LogTrace("Ingredient updated! Ingredient: {@Ingredient}", ingredient);

            return ingredient;
        }

        public async Task<Ingredient> Delete(IngredientResponse model)
        {
            var ingredient = await _ingredientRepository.GetByNameFull(model.Name);
            await _imageRepository.Delete(ingredient.Image);
            await _ingredientRepository.Delete(ingredient);

            _logger.LogTrace("Ingredient deleted! Ingredient: {@Ingredient}", ingredient);

            return ingredient;
        }

        public async Task<(int Updated, int Skipped, int Failed, List<string> FailedNames)> RegenerateImages(IEnumerable<string> excludedNames = null)
        {
            var excluded = new HashSet<string>(
                (excludedNames ?? Enumerable.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var ingredients = await _ingredientRepository.GetAllFull();
            var updated = 0;
            var skipped = 0;
            var failed = 0;
            var failedNames = new List<string>();

            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.Name) || excluded.Contains(ingredient.Name))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var generatedImage = await _ingredientImageGenerator.GenerateAsync(ingredient.Name, ingredient.Description);
                    if (generatedImage == null || string.IsNullOrWhiteSpace(generatedImage.Url))
                    {
                        failed++;
                        failedNames.Add(ingredient.Name);
                        continue;
                    }

                    if (ingredient.Image == null)
                    {
                        ingredient.Image = new Image
                        {
                            Url = generatedImage.Url,
                            Caption = generatedImage.Caption
                        };
                    }
                    else
                    {
                        ingredient.Image.Url = generatedImage.Url;
                        ingredient.Image.Caption = generatedImage.Caption;
                    }

                    await _ingredientRepository.Update(ingredient);
                    updated++;
                }
                catch (Exception ex)
                {
                    failed++;
                    failedNames.Add(ingredient.Name);
                    _logger.LogWarning(ex, "Failed to regenerate image for ingredient {IngredientName}", ingredient.Name);
                }
            }

            return (updated, skipped, failed, failedNames);
        }

        public async Task<(bool Updated, string Error, IngredientResponse Ingredient)> RegenerateImage(string ingredientName)
        {
            if (string.IsNullOrWhiteSpace(ingredientName))
            {
                return (false, "Ingredient name is required.", null);
            }

            var normalizedName = ingredientName.Trim();
            var ingredient = await _ingredientRepository.GetByNameFull(normalizedName);
            if (ingredient == null)
            {
                return (false, $"Ingredient '{normalizedName}' was not found.", null);
            }

            try
            {
                var generatedImage = await _ingredientImageGenerator.GenerateAsync(ingredient.Name, ingredient.Description);
                if (generatedImage == null || string.IsNullOrWhiteSpace(generatedImage.Url))
                {
                    return (false, $"Image generation failed for '{ingredient.Name}'.", null);
                }

                if (ingredient.Image == null)
                {
                    ingredient.Image = new Image
                    {
                        Url = generatedImage.Url,
                        Caption = generatedImage.Caption
                    };
                }
                else
                {
                    ingredient.Image.Url = generatedImage.Url;
                    ingredient.Image.Caption = generatedImage.Caption;
                }

                await _ingredientRepository.Update(ingredient);

                var result = _mapper.Map<IngredientResponse>(ingredient);
                return (true, null, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to regenerate image for ingredient {IngredientName}", ingredient.Name);
                return (false, $"Unexpected error while generating image for '{ingredient.Name}'.", null);
            }
        }

        private static bool IsPlaceholderImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return true;
            }

            var normalized = url.Replace("\\", "/").ToLowerInvariant();
            return normalized.Contains("/assets/images/");
        }
    }
}
