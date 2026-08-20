using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using IngredientResponse = RecipeHub.Domain.Responses.IngredientResponse;

namespace RecipeHub.ApplicationService.Services
{
    public interface IIngredientService
    {
        Task<Ingredient> Create(IngredientViewModel model);
        Task<Ingredient> Get(IngredientResponse model);
        Task<Ingredient> Update(IngredientResponse model);
        Task<Ingredient> Delete(IngredientResponse model);
        Task<IngredientResponse> GetByName(string name);
        Task<IEnumerable<IngredientResponse>> GetAll();
        Task<IEnumerable<IngredientResponse>> GetAllLite();
        Task<(int Updated, int Skipped, int Failed, List<string> FailedNames)> RegenerateImages(IEnumerable<string> excludedNames = null);
        Task<(bool Updated, string Error, IngredientResponse Ingredient)> RegenerateImage(string ingredientName);
    }
}