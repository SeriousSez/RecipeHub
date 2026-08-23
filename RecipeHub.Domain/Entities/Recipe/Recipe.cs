using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeHub.Domain.Entities.Recipe
{
    public class Recipe : BaseEntity
    {
        public string Title { get; set; }
        public User Creator { get; set; }
        public string Description { get; set; }
        public string Instructions { get; set; }
        public string Language { get; set; } = "English";
        public string Portions { get; set; }
        public int? PreparationMinutes { get; set; }
        public int? CookingMinutes { get; set; }
        public int? ChillingMinutes { get; set; }
        public int? CoolingMinutes { get; set; }
        public int? RestingMinutes { get; set; }
        public int? ShelfLifeDays { get; set; }
        public bool? CanBeFrozen { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? Calories { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ProteinGrams { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? CarbohydrateGrams { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? FatGrams { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? FiberGrams { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SugarGrams { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SodiumMilligrams { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();

        public Guid? ImageId { get; set; }

        [ForeignKey(nameof(ImageId))]
        public Image Image { get; set; }
        public List<RecipeIngredient> RecipeIngredients { get; set; }
        public List<RecipeRating> Ratings { get; set; } = new List<RecipeRating>();
    }
}
