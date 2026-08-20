using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class Grocery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_ImageId",
                table: "Recipes");

            migrationBuilder.AddColumn<Guid>(
                name: "GroceryPlanId",
                table: "Recipes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "GroceryLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroceryLists_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GroceryPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroceryPlans_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GroceryIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AmountType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    GroceryListId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroceryIngredients_GroceryLists_GroceryListId",
                        column: x => x.GroceryListId,
                        principalTable: "GroceryLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryIngredients_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_GroceryPlanId",
                table: "Recipes",
                column: "GroceryPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ImageId",
                table: "Recipes",
                column: "ImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroceryIngredients_GroceryListId",
                table: "GroceryIngredients",
                column: "GroceryListId");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryIngredients_ImageId",
                table: "GroceryIngredients",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryLists_UserId",
                table: "GroceryLists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryPlans_UserId",
                table: "GroceryPlans",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_GroceryPlans_GroceryPlanId",
                table: "Recipes",
                column: "GroceryPlanId",
                principalTable: "GroceryPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_GroceryPlans_GroceryPlanId",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "GroceryIngredients");

            migrationBuilder.DropTable(
                name: "GroceryPlans");

            migrationBuilder.DropTable(
                name: "GroceryLists");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_GroceryPlanId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_ImageId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "GroceryPlanId",
                table: "Recipes");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ImageId",
                table: "Recipes",
                column: "ImageId");
        }
    }
}
