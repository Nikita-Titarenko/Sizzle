using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sizzle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonetizationAndAdViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdViewCount",
                table: "Recipes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsStripeProfileConfigured",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaidAdViewCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripeConnectedAccountId",
                table: "AspNetUsers",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StripeProfileConfiguredAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdViewUpdatedAt",
                table: "ApplicationUserRecipes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdEarnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AdViewCount = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    StripeTransferId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdEarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdEarnings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AdEarnings_UserId_CreatedAt",
                table: "AdEarnings",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdEarnings");

            migrationBuilder.DropColumn(
                name: "AdViewCount",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IsStripeProfileConfigured",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PaidAdViewCount",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StripeConnectedAccountId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StripeProfileConfiguredAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AdViewUpdatedAt",
                table: "ApplicationUserRecipes");
        }
    }
}
