using System;

namespace RecipeHub.Domain.Models
{
    public class FoodPlanEntryViewModel
    {
        public Guid? Id { get; set; }
        public string UserId { get; set; }
        public Guid RecipeId { get; set; }
        public DateTime PlannedDate { get; set; }
        public string MealSlot { get; set; }
        public string Notes { get; set; }
        public bool RepeatWeekly { get; set; }
        public DateTime? RepeatUntil { get; set; }
        public int Position { get; set; }
    }
}