namespace RecipeHub.Domain.Entities
{
    public class BaseIngredient : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Language { get; set; } = "English";
    }
}
