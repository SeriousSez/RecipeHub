using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    [DbContext(typeof(RecipeHubContext))]
    [Migration("20260822155000_RepairMissingMyRecipesTheme")]
    public partial class RepairMissingMyRecipesTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @columnExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'UserSettings'
                      AND COLUMN_NAME = 'MyRecipesTheme'
                );
                SET @statement = IF(
                    @columnExists = 0,
                    'ALTER TABLE `UserSettings` ADD COLUMN `MyRecipesTheme` longtext CHARACTER SET utf8mb4 NULL',
                    'SELECT 1'
                );
                PREPARE repairStatement FROM @statement;
                EXECUTE repairStatement;
                DEALLOCATE PREPARE repairStatement;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column is part of the original UserSettings schema, so rollback must preserve it.
        }
    }
}