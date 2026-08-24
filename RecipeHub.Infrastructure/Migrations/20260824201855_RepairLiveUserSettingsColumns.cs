using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class RepairLiveUserSettingsColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "Theme", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "RecipesTheme", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "MyRecipesTheme", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "PreferredLanguage", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "PantryItems", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "UserId", "varchar(255) CHARACTER SET utf8mb4 NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }

        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string columnName, string definition)
        {
            migrationBuilder.Sql($@"
                SET @columnExists = (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserSettings' AND COLUMN_NAME = '{columnName}'
                );
                SET @statement = IF(
                    @columnExists = 0,
                    'ALTER TABLE `UserSettings` ADD COLUMN `{columnName}` {definition}',
                    'SELECT 1'
                );
                PREPARE repairStatement FROM @statement;
                EXECUTE repairStatement;
                DEALLOCATE PREPARE repairStatement;");
        }
    }
}
