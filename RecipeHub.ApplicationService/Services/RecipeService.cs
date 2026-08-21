using AutoMapper;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using RecipeHub.Infrastructure.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using RecipeHub.Domain.Responses;
using System.Linq;
using System;

namespace RecipeHub.ApplicationService.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly ILogger<RecipeService> _logger;
        private readonly IRecipeRepository _recipeRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IRecipeIngredientRepository _recipeIngredientRepository;
        private readonly IImageRepository _imageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public RecipeService(
            ILogger<RecipeService> logger,
            IRecipeRepository recipeRepository,
            IIngredientRepository ingredientRepository,
            IRecipeIngredientRepository recipeIngredientRepository,
            IImageRepository imageRepository,
            IUserRepository userRepository,
            IMapper mapper)
        {
            _logger = logger;
            _recipeRepository = recipeRepository;
            _ingredientRepository = ingredientRepository;
            _recipeIngredientRepository = recipeIngredientRepository;
            _imageRepository = imageRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<RecipeResponse> Create(RecipeViewModel model)
        {
            var user = await _userRepository.GetByUserName(model.Creator);
            if (user == null)
                user = await _userRepository.GetByEmail(model.Creator);

            var recipe = _mapper.Map<Recipe>(model);
            recipe.Creator = user;
            recipe.Categories = model.Categories ?? new List<string>();
            recipe.Tags = model.Tags ?? new List<string>();

            await _recipeRepository.Create(recipe);

            foreach (var ingredient in model.Ingredients)
            {
                var ingredientEntity = _mapper.Map<Ingredient>(ingredient);

                var exists = await _ingredientRepository.Exists(ingredient.Name);
                if (exists == false)
                {
                    ingredientEntity.Language = string.IsNullOrWhiteSpace(ingredient.Language) ? model.Language : ingredient.Language;
                    ingredientEntity = await _ingredientRepository.Create(ingredientEntity);
                }
                else
                {
                    ingredientEntity = await _ingredientRepository.GetByName(ingredient.Name);
                }

                await CreateRecipeIngredient(ingredient.Amount, ingredient.AmountType, recipe, ingredientEntity);
            }

            _logger.LogTrace("Recipe created! Recipe: {@Recipe}", recipe);

            var recipeResponse = _mapper.Map<RecipeResponse>(recipe);
            return recipeResponse;
        }

        public async Task<RecipeResponse> AddIngredients(List<IngredientResponse> ingredients, string title, string creator)
        {
            var recipe = await _recipeRepository.GetByTitleAndCreatorFull(title, creator);
            foreach (var ingredient in ingredients)
            {
                var ingredientEntity = new Ingredient();
                ingredientEntity.Name = ingredient.Name;
                ingredientEntity.Description = ingredient.Description;

                var exists = await _ingredientRepository.Exists(ingredient.Name);
                if (exists == false)
                {
                    ingredientEntity = await _ingredientRepository.Create(ingredientEntity);
                }
                else
                {
                    ingredientEntity = await _ingredientRepository.GetByNameFull(ingredient.Name);
                }

                await CreateRecipeIngredient(ingredient.Amount, ingredient.AmountType, recipe, ingredientEntity);
            }

            _logger.LogTrace("Recipe created! Recipe: {@Recipe}", recipe);

            var recipeResponse = _mapper.Map<RecipeResponse>(recipe);
            return recipeResponse;
        }

        private async Task<RecipeIngredient> CreateRecipeIngredient(decimal amount, string amountType, Recipe recipe, Ingredient ingredient)
        {
            var recipeIngredient = new RecipeIngredient
            {
                Amount = amount,
                AmountType = amountType,
                Recipe = recipe,
                Ingredient = ingredient
            };

            await _recipeIngredientRepository.Create(recipeIngredient);
            return recipeIngredient;
        }

        public async Task<Recipe> Get(RecipeResponse model)
        {
            var recipe = await _recipeRepository.GetByTitleAndCreatorFull(model.Title, model.Creator);

            _logger.LogTrace("Recipe created! Recipe: {@Recipe}", recipe);

            return recipe;
        }

        public async Task<RecipeResponse> Get(Guid id)
        {
            var recipe = await _recipeRepository.GetFull(id);
            if (recipe == null)
                return null;

            var recipeResponse = _mapper.Map<RecipeResponse>(recipe);

            recipeResponse.Ingredients = new List<IngredientResponse>();
            foreach (var recipeIngredient in recipe.RecipeIngredients)
            {
                var ingredientResponse = CreateIngredientResponeModel(recipeIngredient);
                if (ingredientResponse != null)
                    recipeResponse.Ingredients.Add(ingredientResponse);
            }

            return recipeResponse;
        }

        public async Task<RecipeResponse> Get(string title, string creator)
        {
            var recipe = await _recipeRepository.GetByTitleAndCreatorFull(title, creator);
            if (recipe == null)
                return null;

            var recipeResponse = _mapper.Map<RecipeResponse>(recipe);

            recipeResponse.Ingredients = new List<IngredientResponse>();
            foreach (var recipeIngredient in recipe.RecipeIngredients)
            {
                var ingredientResponse = CreateIngredientResponeModel(recipeIngredient);
                if (ingredientResponse != null)
                    recipeResponse.Ingredients.Add(ingredientResponse);
            }

            _logger.LogTrace("Recipe created! Recipe: {@Recipe}", recipeResponse);

            return recipeResponse;
        }

        public async Task<IEnumerable<RecipeResponse>> GetAll()
        {
            var recipes = await _recipeRepository.GetAllFull();

            var recipeList = new List<RecipeResponse>();
            foreach (var recipe in recipes)
            {
                var recipeResponse = _mapper.Map<RecipeResponse>(recipe);
                recipeResponse.Ingredients = recipe.RecipeIngredients == null
                    ? new List<IngredientResponse>()
                    : recipe.RecipeIngredients.Select(CreateIngredientResponeModel).Where(i => i != null).ToList();

                recipeList.Add(recipeResponse);
            }

            return recipeList;
        }

        public async Task<IEnumerable<RecipeResponse>> GetAllWithIngredients()
        {
            var recipes = await _recipeRepository.GetAllWithIngredients();

            var recipeList = new List<RecipeResponse>();
            foreach (var recipe in recipes)
            {
                var recipeResponse = _mapper.Map<RecipeResponse>(recipe);
                recipeResponse.Ingredients = recipe.RecipeIngredients == null
                    ? new List<IngredientResponse>()
                    : recipe.RecipeIngredients.Select(CreateIngredientResponeModel).Where(i => i != null).ToList();

                recipeList.Add(recipeResponse);
            }

            return recipeList;
        }

        public async Task<IEnumerable<RecipeResponse>> GetAll(string creator)
        {
            var user = await _userRepository.GetByUserName(creator);
            if (user == null)
                user = await _userRepository.GetByEmail(creator);

            if (user == null)
                return new List<RecipeResponse>();

            var recipes = await _recipeRepository.GetAllByCreatorId(user.Id);

            var recipeList = new List<RecipeResponse>();
            foreach (var recipe in recipes)
            {
                var recipeResponse = new RecipeResponse
                {
                    Id = recipe.Id,
                    Title = recipe.Title,
                    Creator = user.UserName,
                    Description = recipe.Description,
                    Instructions = recipe.Instructions,
                    Language = recipe.Language,
                    Portions = recipe.Portions,
                    Categories = recipe.Categories ?? new List<string>(),
                    Tags = recipe.Tags ?? new List<string>(),
                    Created = recipe.Created,
                    Image = _mapper.Map<ImageResponse>(recipe.Image),
                    Ingredients = recipe.RecipeIngredients == null
                        ? new List<IngredientResponse>()
                        : recipe.RecipeIngredients.Select(CreateIngredientResponeModel).Where(i => i != null).ToList()
                };

                recipeList.Add(recipeResponse);
            }

            return recipeList;
        }

        public async Task<List<RecipeResponse>> GetAllByIngredient(IngredientResponse model)
        {
            var recipeList = new List<RecipeResponse>();
            var ingredient = await _ingredientRepository.GetByName(model.Name);
            if (ingredient == null)
                return recipeList;

            var recipes = await _recipeRepository.GetAllByIngredient(ingredient.Id);

            foreach (var recipe in recipes)
            {
                var recipeResponse = _mapper.Map<RecipeResponse>(recipe);
                recipeList.Add(recipeResponse);
            }

            return recipeList;
        }

        public async Task<RecipeResponse> Update(RecipeUpdateViewModel model)
        {
            var recipe = await _recipeRepository.GetByTitleAndCreatorFull(model.OldTitle, model.Creator);

            if (model.Ingredients.Any())
            {
                foreach (var ingredient in model.Ingredients)
                {
                    var matchingRecipeIngredient = recipe.RecipeIngredients.FirstOrDefault(r => r.Ingredient.Name == ingredient.Name);
                    if (matchingRecipeIngredient == null)
                    {
                        var entity = await _ingredientRepository.GetByName(ingredient.Name);
                        matchingRecipeIngredient = await _recipeIngredientRepository.GetFullByIngredient(entity);

                        if (matchingRecipeIngredient == null)
                            matchingRecipeIngredient = await CreateRecipeIngredient(ingredient.Amount, ingredient.AmountType, recipe, entity);
                    }

                    var ingredientEntity = await _recipeIngredientRepository.GetFull(matchingRecipeIngredient.Id);
                    ingredientEntity.Amount = ingredient.Amount;
                    ingredientEntity.AmountType = ingredient.AmountType;

                    await _recipeIngredientRepository.Update(ingredientEntity);
                }
            }

            recipe.Title = model.Title;
            if (model.Image == null)
            {
                recipe.Image = null;
            }
            else if (recipe.Image == null)
            {
                recipe.Image = _mapper.Map<Image>(model.Image);
            }
            else
            {
                recipe.Image.Url = model.Image.Url;
                recipe.Image.Caption = model.Image.Caption;
            }
            recipe.Description = model.Description;
            recipe.Instructions = model.Instructions;
            recipe.Portions = model.Portions;
            recipe.Categories = model.Categories ?? new List<string>();
            recipe.Tags = model.Tags ?? new List<string>();
            //recipe.Language = model.Language;
            await _recipeRepository.Update(recipe);

            _logger.LogTrace("Recipe updated! Recipe: {@Recipe}", recipe);

            var response = _mapper.Map<RecipeResponse>(recipe);
            response.Ingredients = new List<IngredientResponse>();
            foreach (var recipeIngredient in recipe.RecipeIngredients)
            {
                var ingredientResponse = CreateIngredientResponeModel(recipeIngredient);
                if (ingredientResponse != null)
                    response.Ingredients.Add(ingredientResponse);
            }

            return response;
        }

        public async Task<bool> DeleteRecipeIngredient(IngredientResponse model)
        {
            var ingredient = await _ingredientRepository.GetByName(model.Name);
            var recipeIngredient = await _recipeIngredientRepository.GetFullByIngredient(ingredient);
            if (recipeIngredient == null)
                return false;

            await _recipeIngredientRepository.Delete(recipeIngredient);

            _logger.LogTrace("RecipeIngredient deleted! RecipeIngredient: {@RecipeIngredient}", recipeIngredient);

            return true;
        }

        public async Task<Recipe> Delete(Guid id)
        {
            var recipe = await _recipeRepository.GetFull(id);

            if (recipe.Image != null)
            {
                await _imageRepository.Delete(recipe.Image);
            }

            if (recipe.RecipeIngredients.Any())
            {
                await _recipeIngredientRepository.DeleteRange(recipe.RecipeIngredients);
            }

            await _recipeRepository.Delete(recipe);

            _logger.LogTrace("Recipe deleted! Recipe: {@Recipe}", recipe);

            return recipe;
        }

        private IngredientResponse CreateIngredientResponeModel(RecipeIngredient recipeIngredient)
        {
            var ingredient = recipeIngredient.Ingredient;
            if (ingredient == null)
            {
                _logger.LogWarning("RecipeIngredient {RecipeIngredientId} has no linked Ingredient; skipping.", recipeIngredient.Id);
                return null;
            }

            var response = new IngredientResponse
            {
                Name = ingredient.Name,
                Description = ingredient.Description,
                Amount = recipeIngredient.Amount,
                AmountType = recipeIngredient.AmountType,
                Image = null,
                Created = recipeIngredient.Created
            };

            return response;
        }
    }
}
