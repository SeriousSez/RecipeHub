using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddPlannedRecipes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlannedRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlannedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MealSlot = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RepeatWeekly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RepeatUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedRecipes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("SET @planned_recipe_user_id_charset = (SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @planned_recipe_user_id_collation = (SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @planned_recipe_user_id_sql = CONCAT('ALTER TABLE `PlannedRecipes` MODIFY COLUMN `UserId` varchar(255) CHARACTER SET ', @planned_recipe_user_id_charset, ' COLLATE ', @planned_recipe_user_id_collation, ' NOT NULL');");
            migrationBuilder.Sql("PREPARE planned_recipe_user_id_statement FROM @planned_recipe_user_id_sql;");
            migrationBuilder.Sql("EXECUTE planned_recipe_user_id_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE planned_recipe_user_id_statement;");

            migrationBuilder.Sql("SET @planned_recipe_recipe_id_charset = (SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @planned_recipe_recipe_id_collation = (SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @planned_recipe_recipe_id_sql = CONCAT('ALTER TABLE `PlannedRecipes` MODIFY COLUMN `RecipeId` char(36) CHARACTER SET ', @planned_recipe_recipe_id_charset, ' COLLATE ', @planned_recipe_recipe_id_collation, ' NOT NULL');");
            migrationBuilder.Sql("PREPARE planned_recipe_recipe_id_statement FROM @planned_recipe_recipe_id_sql;");
            migrationBuilder.Sql("EXECUTE planned_recipe_recipe_id_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE planned_recipe_recipe_id_statement;");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedRecipes_AspNetUsers_UserId",
                table: "PlannedRecipes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedRecipes_Recipes_RecipeId",
                table: "PlannedRecipes",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedRecipes_RecipeId",
                table: "PlannedRecipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedRecipes_UserId_PlannedDate",
                table: "PlannedRecipes",
                columns: new[] { "UserId", "PlannedDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannedRecipes");
        }
    }
}
