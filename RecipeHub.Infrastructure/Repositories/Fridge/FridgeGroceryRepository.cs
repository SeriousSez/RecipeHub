using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities.Fridge;
using RecipeHub.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories.Fridge
{
    public class FridgeGroceryRepository : BaseRepository<FridgeGrocery>, IFridgeGroceryRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public FridgeGroceryRepository(RecipeHubContext db) : base(db) { }

        public async Task<ICollection<FridgeGrocery>> GetByFridgeId(Guid id)
        {
            return await _dbSet.Where(f => f.FridgeId == id).ToListAsync();
        }
    }
}
