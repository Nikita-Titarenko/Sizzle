using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sizzle.Infrastructure.Migrations
{
    public partial class AddIngredientTranslations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientTranslations",
                columns: table => new
                {
                    IngredientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    LanguageCode = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientTranslations", x => new { x.IngredientId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_IngredientTranslations_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.Sql("INSERT INTO `IngredientTranslations` (`IngredientId`, `LanguageCode`, `Name`) SELECT `Id`, 'en', `Name` FROM `Ingredients`;");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Ingredients");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Ingredients",
                type: "longtext",
                nullable: false);

            migrationBuilder.Sql("UPDATE `Ingredients` i INNER JOIN `IngredientTranslations` t ON i.`Id` = t.`IngredientId` AND t.`LanguageCode` = 'en' SET i.`Name` = t.`Name`;");

            migrationBuilder.DropTable(
                name: "IngredientTranslations");
        }
    }
}
