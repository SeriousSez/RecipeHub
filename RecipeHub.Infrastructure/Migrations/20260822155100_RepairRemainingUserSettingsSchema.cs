using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    [DbContext(typeof(RecipeHubContext))]
    [Migration("20260822155100_RepairRemainingUserSettingsSchema")]
    public partial class RepairRemainingUserSettingsSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "Theme", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "RecipesTheme", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "MyRecipesTheme", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "PreferredLanguage", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "PantryItems", "longtext CHARACTER SET utf8mb4 NULL");

            migrationBuilder.Sql(@"
                SET @userIdExists = (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserSettings' AND COLUMN_NAME = 'UserId'
                );
                SET @identityIdExists = (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserSettings' AND COLUMN_NAME = 'IdentityId'
                );
                SET @statement = IF(
                    @userIdExists > 0,
                    'SELECT 1',
                    IF(
                        @identityIdExists > 0,
                        'ALTER TABLE `UserSettings` CHANGE COLUMN `IdentityId` `UserId` varchar(255) CHARACTER SET utf8mb4 NULL',
                        'ALTER TABLE `UserSettings` ADD COLUMN `UserId` varchar(255) CHARACTER SET utf8mb4 NULL'
                    )
                );
                PREPARE repairStatement FROM @statement;
                EXECUTE repairStatement;
                DEALLOCATE PREPARE repairStatement;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These columns belong to the current and historical UserSettings schema.
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