using System;

namespace RecipeHub.Domain.Responses
{
    public class IngredientResponse
    {
        public Guid Id { get; set; }
        public Guid? RecipeIngredientId { get; set; }
        public Guid? GroupId { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Language { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public string Group { get; set; }
        public DateTime Created { get; set; }
        public ImageResponse Image { get; set; }
    }
}
