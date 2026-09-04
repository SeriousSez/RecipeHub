using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeHub.Domain.Entities.Plan
{
    public class PlannedRecipe : BaseEntity
    {
        public string UserId { get; set; }
        public User User { get; set; }
        public Guid RecipeId { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public Recipe.Recipe Recipe { get; set; }
        public DateTime PlannedDate { get; set; }
        public string MealSlot { get; set; }
        public int Servings { get; set; } = 1;
        public string Notes { get; set; }
        public bool RepeatWeekly { get; set; }
        public DateTime? RepeatUntil { get; set; }
        public int Position { get; set; }
    }
}