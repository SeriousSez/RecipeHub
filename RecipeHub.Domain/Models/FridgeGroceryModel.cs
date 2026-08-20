using System;

namespace RecipeHub.Domain.Models
{
    public class FridgeGroceryModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string AmountType { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public ImageViewModel Image { get; set; }
        public Guid FridgeId { get; set; }
    }
}
