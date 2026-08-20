using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Recipe;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IFavoriteRepository : IBaseRepository<Favorites>
    {
        Task<bool> RecipeFavoriteExists(User user, Recipe recipe);
        Task<bool> IngredientFavoriteExists(User user, Ingredient ingredient);
        Task<Favorites> GetByUser(User user);
        Task<Favorites> GetByUserFull(User user);
    }
}