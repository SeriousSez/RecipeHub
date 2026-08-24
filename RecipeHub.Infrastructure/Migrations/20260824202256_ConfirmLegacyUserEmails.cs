using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class ConfirmLegacyUserEmails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `AspNetUsers` SET `EmailConfirmed` = 1 WHERE `EmailConfirmed` = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
