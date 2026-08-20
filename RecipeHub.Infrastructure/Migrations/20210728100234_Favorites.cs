using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class Favorites : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FavoritesId",
                table: "Recipes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "FavoritesId",
                table: "Ingredients",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_FavoritesId",
                table: "Recipes",
                column: "FavoritesId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_FavoritesId",
                table: "Ingredients",
                column: "FavoritesId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId",
                table: "Favorites",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Favorites_FavoritesId",
                table: "Ingredients",
                column: "FavoritesId",
                principalTable: "Favorites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Favorites_FavoritesId",
                table: "Recipes",
                column: "FavoritesId",
                principalTable: "Favorites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Favorites_FavoritesId",
                table: "Ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Favorites_FavoritesId",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_FavoritesId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_FavoritesId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "FavoritesId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "FavoritesId",
                table: "Ingredients");
        }
    }
}
