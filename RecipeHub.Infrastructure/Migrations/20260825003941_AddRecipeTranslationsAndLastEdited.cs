using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddRecipeTranslationsAndLastEdited : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "UserSettings", "Recipes", "RecipeRatings", "RecipeIngredients", "PublicProfiles", "IngredientTranslations", "Ingredients", "Images", "GroceryPlans", "GroceryLists", "GroceryIngredients", "GroceryCategoryFeedback", "Fridges", "FridgeGroceries", "Favorites" })
                AddLastEditedIfTableExists(migrationBuilder, table);

            migrationBuilder.CreateTable(
                name: "RecipeTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecipeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Language = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceLastEdited = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastEdited = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTranslations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("SET @translation_recipe_id_charset = (SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @translation_recipe_id_collation = (SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @translation_recipe_id_sql = CONCAT('ALTER TABLE `RecipeTranslations` MODIFY COLUMN `RecipeId` char(36) CHARACTER SET ', @translation_recipe_id_charset, ' COLLATE ', @translation_recipe_id_collation, ' NOT NULL');");
            migrationBuilder.Sql("PREPARE translation_recipe_id_statement FROM @translation_recipe_id_sql;");
            migrationBuilder.Sql("EXECUTE translation_recipe_id_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE translation_recipe_id_statement;");
            migrationBuilder.AddForeignKey(
                name: "FK_RecipeTranslations_Recipes_RecipeId",
                table: "RecipeTranslations",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeTranslations_RecipeId_Language",
                table: "RecipeTranslations",
                columns: new[] { "RecipeId", "Language" },
                unique: true);
        }

        private static void AddLastEditedIfTableExists(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($@"
                SET @tableExists = (
                    SELECT COUNT(*) FROM information_schema.TABLES
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}'
                );
                SET @columnExists = (
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'LastEdited'
                );
                SET @statement = IF(@tableExists = 1 AND @columnExists = 0,
                    'ALTER TABLE `{tableName}` ADD COLUMN `LastEdited` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)',
                    'SELECT 1');
                PREPARE lastEditedStatement FROM @statement;
                EXECUTE lastEditedStatement;
                DEALLOCATE PREPARE lastEditedStatement;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeTranslations");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "RecipeRatings");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "PublicProfiles");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "IngredientTranslations");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "GroceryPlans");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "GroceryLists");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "GroceryIngredients");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "GroceryCategoryFeedback");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "Fridges");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "FridgeGroceries");

            migrationBuilder.DropColumn(
                name: "LastEdited",
                table: "Favorites");
        }
    }
}
