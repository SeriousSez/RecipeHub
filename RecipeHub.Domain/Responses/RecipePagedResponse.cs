using System.Collections.Generic;

namespace RecipeHub.Domain.Responses
{
    public class RecipePagedResponse
    {
        public List<RecipeResponse> Items { get; set; } = new List<RecipeResponse>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<string> AvailableTags { get; set; } = new List<string>();
    }
}
