using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    [DbContext(typeof(RecipeHubContext))]
    [Migration("20260822130000_AddStructuredPantry")]
    public partial class AddStructuredPantry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PantryItems",
                table: "UserSettings",
                type: "longtext",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PantryItems",
                table: "UserSettings");
        }
    }
}