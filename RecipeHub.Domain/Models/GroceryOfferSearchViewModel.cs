using System;
using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class GroceryOfferSearchViewModel
    {
        public List<string> IngredientNames { get; set; } = new List<string>();
        public Dictionary<string, string> IngredientCategories { get; set; } = new Dictionary<string, string>();
        public string ShoppingPreference { get; set; } = "balanced";
        public bool ForceRefresh { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusKm { get; set; } = 10;
    }

    public class GroceryOfferSearchResponse
    {
        public List<GroceryNearbyStoreViewModel> Stores { get; set; } = new List<GroceryNearbyStoreViewModel>();
        public List<GroceryIngredientOfferViewModel> Offers { get; set; } = new List<GroceryIngredientOfferViewModel>();
        public List<string> UnmatchedIngredients { get; set; } = new List<string>();
        public DateTime GeneratedAtUtc { get; set; }
    }

    public class GroceryNearbyStoreViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ChainName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceKm { get; set; }
    }

    public class GroceryIngredientOfferViewModel
    {
        public string IngredientName { get; set; }
        public string ProductName { get; set; }
        public string ProductId { get; set; }
        public string OfferId { get; set; }
        public string ProductUrl { get; set; }
        public string ChainName { get; set; }
        public string StoreName { get; set; }
        public string StoreAddress { get; set; }
        public string StoreCity { get; set; }
        public string StorePostalCode { get; set; }
        public double DistanceKm { get; set; }
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int? DiscountPercentage { get; set; }
        public string Currency { get; set; }
        public string PriceKind { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string ImageUrl { get; set; }
    }
}