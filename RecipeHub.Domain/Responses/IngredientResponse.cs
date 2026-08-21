using System;

namespace RecipeHub.Domain.Responses
{
    public class IngredientResponse
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Language { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public DateTime Created { get; set; }
        public ImageResponse Image { get; set; }
    }
}
