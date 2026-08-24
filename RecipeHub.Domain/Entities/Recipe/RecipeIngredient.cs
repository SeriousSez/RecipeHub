namespace RecipeHub.Domain.Entities.Recipe
{
    public class RecipeIngredient : BaseEntity
    {
        public System.Guid? GroupId { get; set; }
        public int GroupOrder { get; set; }
        public int IngredientOrder { get; set; }
        public Recipe Recipe { get; set; }
        public Ingredient Ingredient { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public string Group { get; set; }
    }
}
