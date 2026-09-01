using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class RecipeGenerationRequest
    {
        public string Prompt { get; set; }
        public List<string> PantryItems { get; set; } = new List<string>();
        public string Language { get; set; } = "English";
        public string Portions { get; set; }
    }

    public class GeneratedRecipeIngredient
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public string Group { get; set; }
        public int GroupOrder { get; set; }
        public int IngredientOrder { get; set; }
    }

    public class GeneratedRecipeResponse
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Instructions { get; set; }
        public string Portions { get; set; }
        public int? PreparationMinutes { get; set; }
        public int? CookingMinutes { get; set; }
        public int? ProofingMinutes { get; set; }
        public int? ChillingMinutes { get; set; }
        public int? CoolingMinutes { get; set; }
        public int? RestingMinutes { get; set; }
        public int? ShelfLifeDays { get; set; }
        public bool? CanBeFrozen { get; set; }
        public decimal? Calories { get; set; }
        public decimal? ProteinGrams { get; set; }
        public decimal? CarbohydrateGrams { get; set; }
        public decimal? FatGrams { get; set; }
        public decimal? FiberGrams { get; set; }
        public decimal? SugarGrams { get; set; }
        public decimal? SodiumMilligrams { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<GeneratedRecipeIngredient> Ingredients { get; set; } = new List<GeneratedRecipeIngredient>();
        public string Provider { get; set; }
        public string ErrorCode { get; set; }
    }
}
