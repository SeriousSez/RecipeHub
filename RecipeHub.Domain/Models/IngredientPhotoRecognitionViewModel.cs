using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class IngredientPhotoImage
    {
        public string ImageBase64 { get; set; }
        public string ContentType { get; set; } = "image/jpeg";
    }

    public class IngredientPhotoRecognitionRequest
    {
        public List<IngredientPhotoImage> Images { get; set; } = new List<IngredientPhotoImage>();
        public string Language { get; set; } = "English";
    }

    public class RecognizedPantryItem
    {
        public string Name { get; set; }
        public decimal? Amount { get; set; }
        public string AmountType { get; set; }
        public string ExpirationDate { get; set; }
    }

    public class IngredientPhotoRecognitionResponse
    {
        public List<RecognizedPantryItem> Items { get; set; } = new List<RecognizedPantryItem>();
        public string Provider { get; set; }
        public string ErrorCode { get; set; }
    }
}
