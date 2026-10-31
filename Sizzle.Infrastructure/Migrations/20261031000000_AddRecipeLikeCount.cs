using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sizzle.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031000000_AddRecipeLikeCount")]
public sealed class AddRecipeLikeCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "LikeCount",
            table: "Recipes",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("UPDATE `Recipes` AS r SET r.`LikeCount` = (SELECT COUNT(*) FROM `ApplicationUserRecipes` AS aur WHERE aur.`RecipeId` = r.`Id` AND aur.`IsLiked` = 1)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LikeCount", table: "Recipes");
    }
}