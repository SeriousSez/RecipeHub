using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeHub.Domain.Entities;
using System;
using System.Linq;

namespace RecipeHub.Infrastructure.Helpers
{
    public class DataGenerator
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            //using (var context = new RecipeHubContext(serviceProvider.GetRequiredService<DbContextOptions<RecipeHubContext>>()))
            //{
            //    // Look for any board games.
            //    if (context.Users.Any())
            //    {
            //        return;   // Data was already seeded
            //    }

            //    context.Users.AddRange(
            //        new User
            //        {

            //        });

            //    context.SaveChanges();
            //}
        }
    }
}
