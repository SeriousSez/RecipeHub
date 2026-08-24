using System.Collections.Generic;
using System;

namespace RecipeHub.Domain.Entities.Recipe
{
    public class Favorites : BaseEntity
    {
        public string UserId { get; set; }
        public User User { get; set; }
        public Guid? RecipeId { get; set; }
        public List<Recipe> Recipes { get; set; }
        public List<Ingredient> Ingredients { get; set; }
    }
}
