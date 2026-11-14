using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sizzle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientIsBasic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBasic",
                table: "Ingredients",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBasic",
                table: "Ingredients");
        }
    }
}
