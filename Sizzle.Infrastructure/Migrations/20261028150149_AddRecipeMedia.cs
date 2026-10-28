using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sizzle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "RecipeSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Recipes",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeStepFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RecipeStepId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FilePath = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeStepFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeStepFiles_RecipeSteps_RecipeStepId",
                        column: x => x.RecipeStepId,
                        principalTable: "RecipeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeStepFiles_RecipeStepId",
                table: "RecipeStepFiles",
                column: "RecipeStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeStepFiles");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "RecipeSteps");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Recipes");
        }
    }
}
