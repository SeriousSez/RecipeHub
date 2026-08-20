using AutoMapper;
using Microsoft.Extensions.Logging;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using RecipeHub.Infrastructure.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly ILogger<FavoriteService> _logger;
        private readonly IFavoriteRepository _favoriteRepository;
        private readonly IUserRepository _userRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IRecipeRepository _recipeRepository;
        private readonly IMapper _mapper;

        public FavoriteService(
            ILogger<FavoriteService> logger,
            IFavoriteRepository favoriteRepository,
            IUserRepository userRepository,
            IIngredientRepository ingredientRepository,
            IRecipeRepository recipeRepository,
            IMapper mapper)
        {
            _logger = logger;
            _favoriteRepository = favoriteRepository;
            _userRepository = userRepository;
            _ingredientRepository = ingredientRepository;
            _recipeRepository = recipeRepository;
            _mapper = mapper;
        }

        public async Task<FavoritesResponse> Get(string username)
        {
            var user = await _userRepository.GetByUserName(username);
            var favorites = await _favoriteRepository.GetByUserFull(user);

            return _mapper.Map<FavoritesResponse>(favorites);
        }

        public async Task<bool> IsFavored(string username, string title, string creator)
        {
            var user = await _userRepository.GetByUserName(username);

            var recipe = await _recipeRepository.GetByTitleAndCreator(title, creator);
            if (recipe == null)
                return false;

            var isFavored = await _favoriteRepository.RecipeFavoriteExists(user, recipe);
            return isFavored;
        }

        public async Task<Favorites> Create(User user, Recipe recipe = null, Ingredient ingredient = null)
        {
            var favorites = new Favorites
            {
                User = user,
                RecipeId = recipe?.Id,
                Recipes = recipe == null ? new List<Recipe>() : new List<Recipe> { recipe },
                Ingredients = ingredient == null ? new List<Ingredient>() : new List<Ingredient> { ingredient }
            };

            return await _favoriteRepository.Create(favorites);
        }

        public async Task<FavoritesResponse> Recipe(FavoriteRecipeViewModel model)
        {
            var user = await _userRepository.GetByUserName(model.UserName);
            if (user == null)
                return null;

            var recipe = await _recipeRepository.GetByTitleAndCreator(model.Recipe.Title, model.Recipe.Creator);
            if (recipe == null)
                return null;

            var favorites = await _favoriteRepository.GetByUserFull(user);
            if (favorites == null)
            {
                favorites = await Create(user, recipe: recipe);
                _logger.LogTrace("Added Recipe to Favorites on create! Favorites: {@Favorites}", favorites);
                return _mapper.Map<FavoritesResponse>(favorites);
            }

            var exists = await _favoriteRepository.RecipeFavoriteExists(user, recipe);
            if (exists)
            {
                favorites.Recipes.Remove(recipe);
                _logger.LogTrace("Removed Recipe to Favorites! Favorites: {@Favorites}", favorites);
            }
            else
            {
                if (favorites.Recipes == null)
                    favorites.Recipes = new List<Recipe>();

                favorites.Recipes.Add(recipe);
                _logger.LogTrace("Added Recipe to Favorites! Favorites: {@Favorites}", favorites);
            }

            await _favoriteRepository.Update(favorites);

            return _mapper.Map<FavoritesResponse>(favorites);
        }

        public async Task<FavoritesResponse> Ingredient(FavoriteIngredientViewModel model)
        {
            var user = await _userRepository.GetByUserName(model.UserName);
            if (user == null)
                return null;

            var ingredient = await _ingredientRepository.GetByName(model.Ingredient.Name);
            if (ingredient == null)
                return null;

            var favorites = await _favoriteRepository.GetByUserFull(user);
            if (favorites == null)
            {
                favorites = await Create(user, ingredient: ingredient);
                _logger.LogTrace("Added Ingredient to Favorites on create! Favorites: {@Favorites}", favorites);
                return _mapper.Map<FavoritesResponse>(favorites);
            }

            var exists = await _favoriteRepository.IngredientFavoriteExists(user, ingredient);
            if (exists)
            {
                favorites.Ingredients.Remove(ingredient);
                _logger.LogTrace("Removed Ingredient from Favorites! Favorites: {@Favorites}", favorites);
            }
            else
            {
                if (favorites.Ingredients == null)
                    favorites.Ingredients = new List<Ingredient>();

                favorites.Ingredients.Add(ingredient);
                _logger.LogTrace("Added Ingredient from Favorites! Favorites: {@Favorites}", favorites);
            }

            await _favoriteRepository.Update(favorites);

            _logger.LogTrace("Added Ingredient to Favorites! Favorites: {@Favorites}", favorites);

            return _mapper.Map<FavoritesResponse>(favorites);
        }
    }
}
