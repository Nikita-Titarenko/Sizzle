using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sizzle.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031030000_AddSharedRecipes")]
public sealed class AddSharedRecipes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsShared",
            table: "ApplicationUserRecipes",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "IsSharedAt",
            table: "ApplicationUserRecipes",
            type: "datetime(6)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsSharedAt", table: "ApplicationUserRecipes");
        migrationBuilder.DropColumn(name: "IsShared", table: "ApplicationUserRecipes");
    }
}