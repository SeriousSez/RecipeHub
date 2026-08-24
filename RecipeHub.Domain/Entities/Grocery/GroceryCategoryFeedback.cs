using System;

namespace RecipeHub.Domain.Entities.Grocery
{
    public class GroceryCategoryFeedback : BaseEntity
    {
        public string IngredientName { get; set; }
        public string Category { get; set; }
        public int ApprovalCount { get; set; }
        public int RejectionCount { get; set; }
    }
}