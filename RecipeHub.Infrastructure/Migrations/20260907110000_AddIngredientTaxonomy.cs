using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using RecipeHub.Infrastructure;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    [DbContext(typeof(RecipeHubContext))]
    [Migration("20260907110000_AddIngredientTaxonomy")]
    public partial class AddIngredientTaxonomy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Ingredients", "GroceryIngredients" })
            {
                AddColumnIfMissing(migrationBuilder, table, "CanonicalName");
                AddColumnIfMissing(migrationBuilder, table, "Category");
                AddColumnIfMissing(migrationBuilder, table, "Subcategory");
                AddColumnIfMissing(migrationBuilder, table, "OpenFoodFactsId");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Ingredients", "GroceryIngredients" })
            {
                DropColumnIfPresent(migrationBuilder, table, "CanonicalName");
                DropColumnIfPresent(migrationBuilder, table, "Category");
                DropColumnIfPresent(migrationBuilder, table, "Subcategory");
                DropColumnIfPresent(migrationBuilder, table, "OpenFoodFactsId");
            }
        }

        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($"SET @taxonomy_sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE `{table}` ADD `{column}` longtext CHARACTER SET utf8mb4 NULL', 'SELECT 1') FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}');");
            migrationBuilder.Sql("PREPARE taxonomy_stmt FROM @taxonomy_sql;");
            migrationBuilder.Sql("EXECUTE taxonomy_stmt;");
            migrationBuilder.Sql("DEALLOCATE PREPARE taxonomy_stmt;");
        }

        private static void DropColumnIfPresent(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($"SET @taxonomy_sql = (SELECT IF(COUNT(*) > 0, 'ALTER TABLE `{table}` DROP COLUMN `{column}`', 'SELECT 1') FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}');");
            migrationBuilder.Sql("PREPARE taxonomy_stmt FROM @taxonomy_sql;");
            migrationBuilder.Sql("EXECUTE taxonomy_stmt;");
            migrationBuilder.Sql("DEALLOCATE PREPARE taxonomy_stmt;");
        }
    }
}
