using RecipeHub.Domain.Responses;

namespace RecipeHub.Domain.Models
{
    public class FavoriteRecipeViewModel
    {
        public string UserName { get; set; }
        public RecipeResponse Recipe { get; set; }
    }
}
