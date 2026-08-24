using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    [DbContext(typeof(RecipeHubContext))]
    [Migration("20260824203000_MakeFavoriteRecipeIdNullable")]
    public partial class MakeFavoriteRecipeIdNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET @recipe_fk_drop_sql = IFNULL((SELECT CONCAT('ALTER TABLE `Favorites` DROP FOREIGN KEY `', CONSTRAINT_NAME, '`') FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Favorites' AND REFERENCED_TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'RecipeId' LIMIT 1), 'SELECT 1');");
            migrationBuilder.Sql("PREPARE recipe_fk_drop_statement FROM @recipe_fk_drop_sql;");
            migrationBuilder.Sql("EXECUTE recipe_fk_drop_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE recipe_fk_drop_statement;");
            migrationBuilder.Sql("SET @recipe_id_charset = (SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @recipe_id_collation = (SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Recipes' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @favorites_recipe_id_sql = CONCAT('ALTER TABLE `Favorites` MODIFY COLUMN `RecipeId` char(36) CHARACTER SET ', @recipe_id_charset, ' COLLATE ', @recipe_id_collation, ' NULL');");
            migrationBuilder.Sql("PREPARE favorites_recipe_id_statement FROM @favorites_recipe_id_sql;");
            migrationBuilder.Sql("EXECUTE favorites_recipe_id_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE favorites_recipe_id_statement;");
            migrationBuilder.Sql("SET @recipe_fk_add_sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Favorites' AND COLUMN_NAME = 'RecipeId') > 0, 'ALTER TABLE `Favorites` ADD CONSTRAINT `FK_Favorites_Recipes_RecipeId` FOREIGN KEY (`RecipeId`) REFERENCES `Recipes` (`Id`) ON DELETE SET NULL', 'SELECT 1');");
            migrationBuilder.Sql("PREPARE recipe_fk_add_statement FROM @recipe_fk_add_sql;");
            migrationBuilder.Sql("EXECUTE recipe_fk_add_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE recipe_fk_add_statement;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Favorites` MODIFY COLUMN `RecipeId` char(36) NOT NULL;");
        }
    }
}
