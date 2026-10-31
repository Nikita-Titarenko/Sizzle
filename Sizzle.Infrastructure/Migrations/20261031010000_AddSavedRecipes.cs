using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sizzle.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031010000_AddSavedRecipes")]
public sealed class AddSavedRecipes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSaved",
            table: "ApplicationUserRecipes",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "IsSavedAt",
            table: "ApplicationUserRecipes",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationUserRecipes_ApplicationUserId_IsSaved_IsSavedAt",
            table: "ApplicationUserRecipes",
            columns: new[] { "ApplicationUserId", "IsSaved", "IsSavedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ApplicationUserRecipes_ApplicationUserId_IsSaved_IsSavedAt",
            table: "ApplicationUserRecipes");

        migrationBuilder.DropColumn(name: "IsSavedAt", table: "ApplicationUserRecipes");
        migrationBuilder.DropColumn(name: "IsSaved", table: "ApplicationUserRecipes");
    }
}