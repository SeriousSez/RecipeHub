using System;

namespace RecipeHub.Domain.Models
{
    public class RecipeEngagementViewModel
    {
        public Guid RecipeId { get; set; }
        public int? Rating { get; set; }
    }
}