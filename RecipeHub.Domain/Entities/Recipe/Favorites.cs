using System.Collections.Generic;
using System;

namespace RecipeHub.Domain.Entities.Recipe
{
    public class Favorites : BaseEntity
    {
        public User User { get; set; }
        public Guid? RecipeId { get; set; }
        public List<Recipe> Recipes { get; set; }
        public List<Ingredient> Ingredients { get; set; }
    }
}
