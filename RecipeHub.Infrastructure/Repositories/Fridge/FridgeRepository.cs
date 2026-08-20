using Microsoft.EntityFrameworkCore;
using RecipeHub.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories.Fridge
{
    public class FridgeRepository : BaseRepository<Domain.Entities.Fridge.Fridge>, IFridgeRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public FridgeRepository(RecipeHubContext db) : base(db) { }

        public async Task<ICollection<Domain.Entities.Fridge.Fridge>> GetAllByUserId(Guid id)
        {
            return await _dbSet.Include(f => f.User)
                .Where(f => f.User.Id == id.ToString())
                .ToListAsync();
        }
    }
}
