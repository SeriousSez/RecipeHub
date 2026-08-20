namespace RecipeHub.Domain.Entities.Recipe
{
    public class RecipeIngredient : BaseEntity
    {
        public Recipe Recipe { get; set; }
        public Ingredient Ingredient { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
    }
}
