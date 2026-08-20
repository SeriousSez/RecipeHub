using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Recipe;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public class FavoriteRepository : BaseRepository<Favorites>, IFavoriteRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public FavoriteRepository(RecipeHubContext db) : base(db) { }

        public async Task<bool> RecipeFavoriteExists(User user, Recipe recipe)
        {
            if (user == null || recipe == null)
                return false;

            return await _context.Favorites
                .Include(f => f.Recipes)
                .AnyAsync(f => f.User.Id == user.Id && f.Recipes.Any(r => r.Id == recipe.Id));
        }

        public async Task<bool> IngredientFavoriteExists(User user, Ingredient ingredient)
        {
            if (user == null || ingredient == null)
                return false;

            return await _context.Favorites
                .Include(f => f.Ingredients)
                .AnyAsync(f => f.User.Id == user.Id && f.Ingredients.Any(i => i.Id == ingredient.Id));
        }

        public async Task<Favorites> GetByUser(User user)
        {
            if (user == null)
                return null;

            return await _context.Favorites.FirstOrDefaultAsync(f => f.User.Id == user.Id);
        }

        public async Task<Favorites> GetByUserFull(User user)
        {
            if (user == null)
                return null;

            return await _context.Favorites
                .Include(f => f.User)
                .Include(f => f.Recipes)
                    .ThenInclude(r => r.Creator)
                .Include(f => f.Recipes)
                    .ThenInclude(r => r.Image)
                .Include(f => f.Ingredients)
                .FirstOrDefaultAsync(f => f.User.Id == user.Id);
        }
    }
}
