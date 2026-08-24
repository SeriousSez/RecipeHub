using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddRecipeIngredientOrdering : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupOrder",
                table: "RecipeIngredients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IngredientOrder",
                table: "RecipeIngredients",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GroupOrder", table: "RecipeIngredients");
            migrationBuilder.DropColumn(name: "IngredientOrder", table: "RecipeIngredients");
        }
    }
}
