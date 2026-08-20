using RecipeHub.Domain.Entities.Recipe;

namespace RecipeHub.Domain.Entities.Grocery
{
    public class GroceryIngredient : BaseIngredient
    {
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public Image Image { get; set; }
    }
}
