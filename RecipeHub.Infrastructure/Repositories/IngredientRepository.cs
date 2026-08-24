using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities.Recipe;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public class IngredientRepository : BaseRepository<Ingredient>, IIngredientRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public IngredientRepository(RecipeHubContext db) : base(db) { }

        public async Task<bool> Exists(string name)
        {
            return await _context.Ingredients.AnyAsync(i => i.Name == name);
        }

        public async Task<Ingredient> GetFull(Guid id)
        {
            return await _context.Ingredients
                .Include(i => i.Image)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Ingredient> GetByName(string name)
        {
            return await _context.Ingredients.FirstOrDefaultAsync(i => i.Name == name);
        }

        public async Task<Ingredient> GetByNameFull(string name)
        {
            return await _context.Ingredients.Include(i => i.Image).FirstOrDefaultAsync(i => i.Name == name);
        }

        public async Task<IEnumerable<Ingredient>> GetAllLite()
        {
            return await _context.Ingredients.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Ingredient>> GetAllFull()
        {
            return await _context.Ingredients.Include(i => i.Image).ToListAsync();
        }
    }
}
