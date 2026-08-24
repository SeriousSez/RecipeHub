using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHub.Infrastructure.Migrations
{
    public partial class AddApprovalCountToGroceryCategoryFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "GroceryCategoryFeedback",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "GroceryCategoryFeedback");
        }
    }
}
