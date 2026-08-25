using System;
using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class PublicProfileUpdateViewModel
    {
        public Guid UserId { get; set; }
        public string Bio { get; set; }
        public bool IsPublic { get; set; }
        public string ProfileTheme { get; set; }
        public List<Guid> FeaturedRecipeIds { get; set; } = new List<Guid>();
    }
}
