using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddRecipeNutrition : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calories",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CarbohydrateGrams",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatGrams",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FiberGrams",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProteinGrams",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SodiumMilligrams",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SugarGrams",
                table: "Recipes",
                type: "decimal(10,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calories",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CarbohydrateGrams",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "FatGrams",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "FiberGrams",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ProteinGrams",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SodiumMilligrams",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SugarGrams",
                table: "Recipes");
        }
    }
}
