using System.Collections.Generic;

namespace RecipeHub.Domain.Responses
{
    public class PublicProfileResponse
    {
        public UserResponse User { get; set; }
        public string Bio { get; set; }
        public bool IsPublic { get; set; }
        public string ProfileTheme { get; set; }
        public List<RecipeResponse> FeaturedRecipes { get; set; } = new List<RecipeResponse>();
    }
}
