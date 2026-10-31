using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sizzle.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261030190000_AddSubscriptionOrders")]
public sealed class AddSubscriptionOrders : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SubscriptionOrders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                Plan = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                StartsAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubscriptionOrders", x => x.Id);
                table.ForeignKey(
                    name: "FK_SubscriptionOrders_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionOrders_UserId_IsPaid_ExpiresAt",
            table: "SubscriptionOrders",
            columns: new[] { "UserId", "IsPaid", "ExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SubscriptionOrders");
    }
}