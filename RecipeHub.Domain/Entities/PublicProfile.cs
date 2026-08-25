using System.Collections.Generic;

namespace RecipeHub.Domain.Entities
{
    public class PublicProfile : BaseEntity
    {
        public string UserId { get; set; }
        public User User { get; set; }
        public string Bio { get; set; }
        public bool IsPublic { get; set; } = true;
        public string ProfileTheme { get; set; } = "Garden";
        public string FeaturedRecipeIds { get; set; } = "[]";
    }
}
