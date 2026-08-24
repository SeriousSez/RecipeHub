namespace RecipeHub.Domain.Models
{
    public class IngredientTranslationUpdateRequest
    {
        public string IngredientName { get; set; }
        public string Language { get; set; }
        public string TranslatedName { get; set; }
    }
}
