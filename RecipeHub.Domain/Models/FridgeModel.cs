using System;

namespace RecipeHub.Domain.Models
{
    public class FridgeModel
    {
        public string Name { get; set; }
        public DateTime Purchased { get; set; }
        public DateTime Retired { get; set; } = DateTime.MinValue;
    }
}
