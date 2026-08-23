using System;

namespace RecipeHub.Domain.Entities.Recipe
{
    public class RecipeRating : BaseEntity
    {
        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; }
        public string UserId { get; set; }
        public User User { get; set; }
        public int? Rating { get; set; }
    }
}