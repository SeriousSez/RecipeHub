using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Recipe;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IRecipeRepository
    {
        Task Create(Recipe recipe);
        Task<Recipe> Get(Guid id);
        Task<Recipe> GetFull(Guid id);
        Task<Recipe> GetFullByShortId(string shortId);
        Task<Recipe> GetByTitle(string title);
        Task<Recipe> GetByTitleFull(string title);
        Task<Recipe> GetByTitleAndCreator(string title, string creator);
        Task<Recipe> GetByTitleAndCreatorFull(string title, string creator);
        Task<Recipe> GetByCreator(User user);
        Task<IEnumerable<Recipe>> GetAllByCreatorId(string creatorId);
        Task<IEnumerable<Recipe>> GetAll();
        Task<IEnumerable<Recipe>> GetAllByCreatorFull(string creator);
        Task<IEnumerable<Recipe>> GetAllFull();
        Task<IEnumerable<Recipe>> GetAllWithIngredients();
        Task<IEnumerable<Recipe>> GetAllByIngredient(Guid recipeId);
        Task Update(Recipe recipe);
        Task Delete(Recipe recipe);
        Task DeleteRange(List<Recipe> recipes);
    }
}
