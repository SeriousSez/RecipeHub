using RecipeHub.Infrastructure.Interfaces;

namespace RecipeHub.Infrastructure.Repositories.Plan
{
    public class PlanRepository : BaseRepository<Domain.Entities.Plan.GroceryPlan>, IPlanRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public PlanRepository(RecipeHubContext db) : base(db) { }
    }
}
