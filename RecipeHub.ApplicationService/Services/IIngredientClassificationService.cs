using System.Threading.Tasks;
using RecipeHub.Domain.Entities.Recipe;

namespace RecipeHub.ApplicationService.Services
{
    public interface IIngredientClassificationService
    {
        Task<IngredientClassificationResult> ClassifyAsync(string name);
    }

    public sealed class IngredientClassificationResult
    {
        public string CanonicalName { get; init; }
        public string Category { get; init; }
        public string Subcategory { get; init; }
        public string OpenFoodFactsId { get; init; }
    }
}
