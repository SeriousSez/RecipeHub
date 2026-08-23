namespace RecipeHub.Domain.Responses
{
    public class RecipeEngagementResponse
    {
        public int MadeCount { get; set; }
        public decimal? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public bool HasMade { get; set; }
        public int? UserRating { get; set; }
    }
}