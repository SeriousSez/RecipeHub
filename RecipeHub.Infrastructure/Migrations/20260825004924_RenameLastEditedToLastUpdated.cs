using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class RenameLastEditedToLastUpdated : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "UserSettings", "Recipes", "RecipeRatings", "RecipeIngredients", "PublicProfiles", "IngredientTranslations", "Ingredients", "Images", "GroceryPlans", "GroceryLists", "GroceryIngredients", "GroceryCategoryFeedback", "Fridges", "FridgeGroceries", "Favorites" })
                RenameColumnIfPresent(migrationBuilder, table, "LastEdited", "LastUpdated");

            RenameColumnIfPresent(migrationBuilder, "RecipeTranslations", "LastEdited", "LastUpdated");
            RenameColumnIfPresent(migrationBuilder, "RecipeTranslations", "SourceLastEdited", "SourceLastUpdated");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "UserSettings", "Recipes", "RecipeRatings", "RecipeIngredients", "PublicProfiles", "IngredientTranslations", "Ingredients", "Images", "GroceryPlans", "GroceryLists", "GroceryIngredients", "GroceryCategoryFeedback", "Fridges", "FridgeGroceries", "Favorites" })
                RenameColumnIfPresent(migrationBuilder, table, "LastUpdated", "LastEdited");

            RenameColumnIfPresent(migrationBuilder, "RecipeTranslations", "LastUpdated", "LastEdited");
            RenameColumnIfPresent(migrationBuilder, "RecipeTranslations", "SourceLastUpdated", "SourceLastEdited");
        }

        private static void RenameColumnIfPresent(MigrationBuilder migrationBuilder, string tableName, string oldName, string newName)
        {
            migrationBuilder.Sql($@"
                SET @tableExists = (
                    SELECT COUNT(*) FROM information_schema.TABLES
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}'
                );
                SET @oldColumnExists = (
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{oldName}'
                );
                SET @newColumnExists = (
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{newName}'
                );
                SET @statement = IF(@tableExists = 1 AND @oldColumnExists = 1 AND @newColumnExists = 0,
                    'ALTER TABLE `{tableName}` CHANGE COLUMN `{oldName}` `{newName}` datetime(6) NULL',
                    'SELECT 1');
                PREPARE renameStatement FROM @statement;
                EXECUTE renameStatement;
                DEALLOCATE PREPARE renameStatement;");
        }
    }
}
