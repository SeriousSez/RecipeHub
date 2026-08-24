namespace RecipeHub.Domain.Entities.Recipe
{
    public class IngredientTranslation : BaseEntity
    {
        public string IngredientName { get; set; }
        public string Language { get; set; }
        public string TranslatedName { get; set; }
        public string Source { get; set; }
    }
}