using RecipeHub.Domain.Entities.Recipe;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IRecipeIngredientRepository : IBaseRepository<RecipeIngredient>
    {
        Task<RecipeIngredient> GetFull(Guid id);
        Task<RecipeIngredient> GetFullByIngredient(Ingredient ingredient);
        Task<RecipeIngredient> GetFullByRecipe(Recipe recipe);
        Task<List<RecipeIngredient>> GetAllByRecipe(Recipe recipe);
    }
}