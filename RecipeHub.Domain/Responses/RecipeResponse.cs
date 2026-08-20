using System;
using System.Collections.Generic;

namespace RecipeHub.Domain.Responses
{
    public class RecipeResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Creator { get; set; }
        public string Description { get; set; }
        public string Instructions { get; set; }
        public string Language { get; set; }
        public string Portions { get; set; }
        public DateTime Created { get; set; }
        public ImageResponse Image { get; set; }
        public List<IngredientResponse> Ingredients { get; set; }
    }
}
