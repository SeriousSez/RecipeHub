using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    [DbContext(typeof(RecipeHubContext))]
    [Migration("20260822013000_AddRecipeTimingAndStorage")]
    public partial class AddRecipeTimingAndStorage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanBeFrozen",
                table: "Recipes",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CookingMinutes",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationMinutes",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                table: "Recipes",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanBeFrozen",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CookingMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PreparationMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                table: "Recipes");
        }
    }
}