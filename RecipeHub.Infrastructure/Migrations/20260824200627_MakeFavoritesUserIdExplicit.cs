using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class MakeFavoritesUserIdExplicit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET @favorites_fk_drop_sql = IFNULL((SELECT CONCAT('ALTER TABLE `Favorites` DROP FOREIGN KEY `', CONSTRAINT_NAME, '`') FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Favorites' AND REFERENCED_TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'UserId' LIMIT 1), 'SELECT 1');");
            migrationBuilder.Sql("PREPARE favorites_fk_drop_statement FROM @favorites_fk_drop_sql;");
            migrationBuilder.Sql("EXECUTE favorites_fk_drop_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE favorites_fk_drop_statement;");

            migrationBuilder.Sql("DELETE FROM Favorites WHERE UserId IS NULL OR UserId = '';");

            migrationBuilder.Sql("SET @identity_charset = (SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @identity_collation = (SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @favorites_user_id_sql = CONCAT('ALTER TABLE `Favorites` MODIFY `UserId` varchar(255) CHARACTER SET ', @identity_charset, ' COLLATE ', @identity_collation, ' NOT NULL');");
            migrationBuilder.Sql("PREPARE favorites_user_id_statement FROM @favorites_user_id_sql;");
            migrationBuilder.Sql("EXECUTE favorites_user_id_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE favorites_user_id_statement;");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_AspNetUsers_UserId",
                table: "Favorites",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_AspNetUsers_UserId",
                table: "Favorites");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Favorites",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_AspNetUsers_UserId",
                table: "Favorites",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
