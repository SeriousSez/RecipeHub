using System;
using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class GroceryListViewModel
    {
        public Guid UserId { get; set; }
        public List<IngredientViewModel> Ingredients { get; set; }
    }
}
