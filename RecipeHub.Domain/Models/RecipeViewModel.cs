using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class RecipeViewModel
    {
        public string Title { get; set; }
        public string Creator { get; set; }
        public string Description { get; set; }
        public string Instructions { get; set; }
        public string Language { get; set; } = "English";
        public string Portions { get; set; }

        public ImageViewModel Image { get; set; }
        public List<IngredientViewModel> Ingredients { get; set; }
    }
}
