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

        [ForeignKey("ImageId")]
        public Image Image { get; set; }
        public List<RecipeIngredient> RecipeIngredients { get; set; }
    }
}
