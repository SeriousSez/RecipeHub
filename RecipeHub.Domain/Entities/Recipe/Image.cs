namespace RecipeHub.Domain.Entities.Recipe
{
    public class Image : BaseEntity
    {
        public string Url { get; set; }
        public string Caption { get; set; }
    }
}
