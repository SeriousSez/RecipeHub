using RecipeHub.Domain.Responses;

namespace RecipeHub.Domain.Models
{
    public class FavoriteIngredientViewModel
    {
        public string UserName { get; set; }
        public IngredientResponse Ingredient { get; set; }
    }
}
