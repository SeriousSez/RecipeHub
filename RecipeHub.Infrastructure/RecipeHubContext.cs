using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Fridge;
using RecipeHub.Domain.Entities.Grocery;
using RecipeHub.Domain.Entities.Plan;
using RecipeHub.Domain.Entities.Recipe;

namespace RecipeHub.Infrastructure
{
    public class RecipeHubContext : IdentityDbContext<User>
    {
        public IConfiguration Configuration { get; }

        public RecipeHubContext()
        {

        }

        public RecipeHubContext(DbContextOptions<RecipeHubContext> options) : base(options)
        {
        }

        public RecipeHubContext(DbContextOptions<RecipeHubContext> options, IConfiguration configuration) : base(options)
        {
            Configuration = configuration;
        }

        public DbSet<UserSeeker> UserSeekers { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
        public DbSet<RecipeRating> RecipeRatings { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<IngredientTranslation> IngredientTranslations { get; set; }
        public DbSet<Favorites> Favorites { get; set; }

        public DbSet<GroceryPlan> GroceryPlans { get; set; }

        public DbSet<GroceryList> GroceryLists { get; set; }
        public DbSet<GroceryIngredient> GroceryIngredients { get; set; }
        public DbSet<GroceryCategoryFeedback> GroceryCategoryFeedback { get; set; }

        public DbSet<Fridge> Fridges { get; set; }
        public DbSet<FridgeGrocery> FridgeGroceries { get; set; }

        public DbSet<Image> Images { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Recipe>()
                .HasOne(recipe => recipe.Creator)
                .WithMany()
                .HasForeignKey("CreatorId")
                .IsRequired(false);

            modelBuilder.Entity<Recipe>()
                .HasOne(recipe => recipe.Image)
                .WithMany()
                .HasForeignKey(recipe => recipe.ImageId)
                .IsRequired(false);

            modelBuilder.Entity<RecipeRating>()
                .HasIndex(rating => new { rating.RecipeId, rating.UserId })
                .IsUnique();

            modelBuilder.Entity<RecipeRating>()
                .Property(rating => rating.UserId)
                .UseCollation("utf8mb4_general_ci");

            modelBuilder.Entity<RecipeRating>()
                .Property(rating => rating.RecipeId)
                .UseCollation("utf8mb4_unicode_ci");

            modelBuilder.Entity<RecipeRating>()
                .HasOne(rating => rating.Recipe)
                .WithMany(recipe => recipe.Ratings)
                .HasForeignKey(rating => rating.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RecipeRating>()
                .HasOne(rating => rating.User)
                .WithMany()
                .HasForeignKey(rating => rating.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroceryCategoryFeedback>()
                .HasIndex(feedback => new { feedback.IngredientName, feedback.Category })
                .IsUnique();

            modelBuilder.Entity<IngredientTranslation>()
                .HasIndex(translation => new { translation.IngredientName, translation.Language })
                .IsUnique();

            var stringListComparer = new ValueComparer<List<string>>(
                (left, right) => left != null && right != null && left.SequenceEqual(right),
                value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
                value => value == null ? new List<string>() : value.ToList());

            modelBuilder.Entity<Recipe>()
                .Property(r => r.Categories)
                .HasConversion(
                    v => string.Join(";", v),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList())
                .Metadata.SetValueComparer(stringListComparer);

            modelBuilder.Entity<Recipe>()
                .Property(r => r.Tags)
                .HasConversion(
                    v => string.Join(";", v),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList())
                .Metadata.SetValueComparer(stringListComparer);
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)½                                   
        //{
        //    string adminId = "02174cf0–9412–-afbf-59f706d72cf6";
        //    string roleId = "341743f0-asd2–42de-afbf-59kmkkmk72cf6";

        //    //seed admin role
        //    modelBuilder.Entity<IdentityRole>().HasData(new IdentityRole
        //    {
        //        Name = "Admin",
        //        NormalizedName = "ADMIN",
        //    });

        //    //create user
        //    var user = new User 
        //    { 
        //        Id = Guid.NewGuid().ToString(), 
        //        UserName = "Admin",
        //        NormalizedUserName = "ADMIN",
        //        Firstname = "Admin", 
        //        Lastname = "",
        //        Email = "recipe-hub@sezginsahin.dk",
        //        EmailConfirmed = true,
        //        PasswordHash = "471e6604ad6b4f9b85a81305feefb4f7" 
        //    };

        //    //set user password
        //    PasswordHasher<User> ph = new PasswordHasher<User>();
        //    user.PasswordHash = ph.HashPassword(user, "Admin1!");

        //    //seed user
        //    modelBuilder.Entity<User>().HasData(user);

        //    //set user role to admin
        //    modelBuilder.Entity<IdentityUserRole<string>>().HasData(new IdentityUserRole<string>
        //    {
        //        RoleId = roleId,
        //        UserId = adminId
        //    });
        //}

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#if DEBUG
                optionsBuilder.UseInMemoryDatabase(databaseName: "RecipeHub");
#else
                optionsBuilder.UseMySql(Configuration.GetConnectionString("MySql"), new MySqlServerVersion(new Version(8, 0, 11)));
#endif
            }
        }
    }
}
