using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeHub.Domain.Entities
{
    public class UserSettings : BaseEntity
    {
        [NotMapped]
        public string PreferredLanguage { get; set; } = "English";
        public string Theme { get; set; }
        public string RecipesTheme { get; set; }
        [NotMapped]
        public string MyRecipesTheme { get; set; }
        //public string IngredientsTheme { get; set; }

        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Identity { get; set; }
    }
}
