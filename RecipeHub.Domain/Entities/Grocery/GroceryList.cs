using System.Collections.Generic;

namespace RecipeHub.Domain.Entities.Grocery
{
    public class GroceryList : BaseEntity
    {
        public User User { get; set; }
        public List<GroceryIngredient> Ingredients { get; set; }
    }
}
