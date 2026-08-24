using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddGroceryCategoryFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroceryCategoryFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IngredientName = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RejectionCount = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryCategoryFeedback", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryCategoryFeedback_IngredientName_Category",
                table: "GroceryCategoryFeedback",
                columns: new[] { "IngredientName", "Category" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroceryCategoryFeedback");
        }
    }
}
