using System;

namespace RecipeHub.Domain.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastUpdated { get; set; }

        public BaseEntity()
        {
            Created = DateTime.Now;
        }
    }
}
