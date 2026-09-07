namespace RecipeHub.Domain.Entities
{
    public class BaseIngredient : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Language { get; set; } = "English";
        public string CanonicalName { get; set; }
        public string Category { get; set; } = "other";
        public string Subcategory { get; set; } = "other";
        public string OpenFoodFactsId { get; set; }
    }
}
