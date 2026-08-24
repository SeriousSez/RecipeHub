using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class IngredientTranslationRequest
    {
        public List<string> Names { get; set; } = new List<string>();
        public string Language { get; set; }
        public Dictionary<string, string> Contexts { get; set; } = new Dictionary<string, string>();
    }
}