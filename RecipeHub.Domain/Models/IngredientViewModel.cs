namespace RecipeHub.Domain.Models
{
    public class IngredientViewModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Language { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public string Group { get; set; }
        public ImageViewModel Image { get; set; }
    }
}
