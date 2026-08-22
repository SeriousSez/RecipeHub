using System;

namespace RecipeHub.Domain.Models
{
    public class PantryItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal? Amount { get; set; }
        public string AmountType { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }
}