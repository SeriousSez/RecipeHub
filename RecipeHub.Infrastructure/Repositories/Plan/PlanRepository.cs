using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities.Plan;
using RecipeHub.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories.Plan
{
    public class PlanRepository : BaseRepository<Domain.Entities.Plan.GroceryPlan>, IPlanRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public PlanRepository(RecipeHubContext db) : base(db) { }

        public async Task<PlannedRecipe> CreateEntry(PlannedRecipe entry)
        {
            var entity = await _recipeHubContext.PlannedRecipes.AddAsync(entry);
            await _recipeHubContext.SaveChangesAsync();
            return entity.Entity;
        }

        public async Task<PlannedRecipe> GetEntry(string userId, Guid id)
        {
            return await _recipeHubContext.PlannedRecipes
                .Include(entry => entry.Recipe)
                    .ThenInclude(recipe => recipe.Creator)
                .Include(entry => entry.Recipe)
                    .ThenInclude(recipe => recipe.Image)
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Id == id);
        }

        public async Task<ICollection<PlannedRecipe>> GetEntriesByUserId(string userId, DateTime start, DateTime end)
        {
            return await _recipeHubContext.PlannedRecipes
                .AsNoTracking()
                .Include(entry => entry.Recipe)
                    .ThenInclude(recipe => recipe.Creator)
                .Include(entry => entry.Recipe)
                    .ThenInclude(recipe => recipe.Image)
                .Where(entry => entry.UserId == userId &&
                    ((entry.PlannedDate >= start && entry.PlannedDate <= end) ||
                     (entry.RepeatWeekly && entry.PlannedDate <= end && (!entry.RepeatUntil.HasValue || entry.RepeatUntil.Value >= start))))
                .ToListAsync();
        }

        public async Task UpdateEntry(PlannedRecipe entry)
        {
            _recipeHubContext.PlannedRecipes.Update(entry);
            await _recipeHubContext.SaveChangesAsync();
        }

        public async Task DeleteEntry(PlannedRecipe entry)
        {
            _recipeHubContext.PlannedRecipes.Remove(entry);
            await _recipeHubContext.SaveChangesAsync();
        }
    }
}
