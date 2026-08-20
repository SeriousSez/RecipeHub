using System.Collections.Generic;

namespace RecipeHub.Domain.Responses
{
    public class FavoritesResponse
    {
        public UserResponse User { get; set; }
        public List<RecipeResponse> Recipes { get; set; }
        public List<IngredientResponse> Ingredients { get; set; }
    }
}
