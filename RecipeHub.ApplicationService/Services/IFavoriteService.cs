using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public interface IFavoriteService
    {
        Task<FavoritesResponse> Get(string username);
        Task<bool> IsFavored(string username, string title, string creator);
        Task<FavoritesResponse> Recipe(FavoriteRecipeViewModel model);
        Task<FavoritesResponse> Ingredient(FavoriteIngredientViewModel model);
    }
}