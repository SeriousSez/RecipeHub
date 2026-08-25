using System;

namespace RecipeHub.Domain.Responses
{
    public class FoodPlanEntryResponse
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public Guid RecipeId { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime OccurrenceDate { get; set; }
        public string MealSlot { get; set; }
        public string Notes { get; set; }
        public bool RepeatWeekly { get; set; }
        public DateTime? RepeatUntil { get; set; }
        public int Position { get; set; }
        public RecipeResponse Recipe { get; set; }
    }
}