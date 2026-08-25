using RecipeHub.Domain.Entities.Plan;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Interfaces
{
    public interface IPlanRepository : IBaseRepository<GroceryPlan>
    {
        Task<PlannedRecipe> CreateEntry(PlannedRecipe entry);
        Task<PlannedRecipe> GetEntry(string userId, Guid id);
        Task<ICollection<PlannedRecipe>> GetEntriesByUserId(string userId, DateTime start, DateTime end);
        Task UpdateEntry(PlannedRecipe entry);
        Task DeleteEntry(PlannedRecipe entry);
    }
}
