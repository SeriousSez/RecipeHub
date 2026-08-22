using System;
using System.Collections.Generic;

namespace RecipeHub.Domain.Responses
{
    public class RecipeResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Creator { get; set; }
        public string Description { get; set; }
        public string Instructions { get; set; }
        public string Language { get; set; }
        public string Portions { get; set; }
        public int? PreparationMinutes { get; set; }
        public int? CookingMinutes { get; set; }
        public int? ChillingMinutes { get; set; }
        public int? CoolingMinutes { get; set; }
        public int? RestingMinutes { get; set; }
        public int? ShelfLifeDays { get; set; }
        public bool? CanBeFrozen { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public DateTime Created { get; set; }
        public ImageResponse Image { get; set; }
        public List<IngredientResponse> Ingredients { get; set; }
    }
}
