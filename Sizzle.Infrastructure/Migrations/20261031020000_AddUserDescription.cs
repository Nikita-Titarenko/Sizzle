using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sizzle.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031020000_AddUserDescription")]
public sealed class AddUserDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "AspNetUsers",
            type: "longtext",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Description", table: "AspNetUsers");
    }
}