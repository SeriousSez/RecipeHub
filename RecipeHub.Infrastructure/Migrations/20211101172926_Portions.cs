using Microsoft.EntityFrameworkCore.Migrations;

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class Portions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Portions",
                table: "Recipes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Portions",
                table: "Recipes");
        }
    }
}
