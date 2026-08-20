using System;

namespace RecipeHub.Domain.Responses
{
    public class ImageResponse
    {
        public Guid Id { get; set; }
        public string Url { get; set; }
        public string Caption { get; set; }
    }
}
