using System;

namespace RecipeHub.Domain.Models
{
    public class UserSettingsUpdateViewModel
    {
        public Guid UserId { get; set; }
        public string PreferredLanguage { get; set; }
        public string Theme { get; set; }
        public string RecipesTheme { get; set; }
        public string MyRecipesTheme { get; set; }
        //public string IngredientsTheme { get; set; }
    }
}
