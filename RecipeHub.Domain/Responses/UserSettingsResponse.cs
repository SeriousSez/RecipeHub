namespace RecipeHub.Domain.Responses
{
    public class UserSettingsResponse
    {
        public string PreferredLanguage { get; set; }
        public string Theme { get; set; }
        public string RecipesTheme { get; set; }
        public string MyRecipesTheme { get; set; }
        //public string IngredientsTheme { get; set; }
    }
}
