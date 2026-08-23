using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class NutritionEstimateRequest
    {
        public decimal Portions { get; set; } = 1;
        public string Instructions { get; set; }
        public List<IngredientViewModel> Ingredients { get; set; } = new List<IngredientViewModel>();
    }

    public class NutritionEstimateResponse
    {
        public decimal Calories { get; set; }
        public decimal ProteinGrams { get; set; }
        public decimal CarbohydrateGrams { get; set; }
        public decimal FatGrams { get; set; }
        public decimal FiberGrams { get; set; }
        public decimal SugarGrams { get; set; }
        public decimal SodiumMilligrams { get; set; }
        public int EstimatedIngredientCount { get; set; }
        public string Provider { get; set; }
        public string ErrorCode { get; set; }
        public decimal CoveragePercent { get; set; }
        public List<string> UnmatchedIngredients { get; set; } = new List<string>();
    }
}