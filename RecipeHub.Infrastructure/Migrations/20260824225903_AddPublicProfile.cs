using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddPublicProfile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bio = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublic = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProfileTheme = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FeaturedRecipeIds = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProfiles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("SET @profile_user_id_charset = (SELECT CHARACTER_SET_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @profile_user_id_collation = (SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'Id' LIMIT 1);");
            migrationBuilder.Sql("SET @profile_user_id_sql = CONCAT('ALTER TABLE `PublicProfiles` MODIFY COLUMN `UserId` varchar(255) CHARACTER SET ', @profile_user_id_charset, ' COLLATE ', @profile_user_id_collation, ' NOT NULL');");
            migrationBuilder.Sql("PREPARE profile_user_id_statement FROM @profile_user_id_sql;");
            migrationBuilder.Sql("EXECUTE profile_user_id_statement;");
            migrationBuilder.Sql("DEALLOCATE PREPARE profile_user_id_statement;");

            migrationBuilder.AddForeignKey(
                name: "FK_PublicProfiles_AspNetUsers_UserId",
                table: "PublicProfiles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_PublicProfiles_UserId",
                table: "PublicProfiles",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicProfiles");
        }
    }
}
