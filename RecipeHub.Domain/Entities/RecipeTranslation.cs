using System;

namespace RecipeHub.Domain.Entities
{
    public class RecipeTranslation : BaseEntity
    {
        public Guid RecipeId { get; set; }
        public string Language { get; set; }
        public DateTime? SourceLastUpdated { get; set; }
        public string PayloadJson { get; set; }
        public Recipe.Recipe Recipe { get; set; }
    }
}
