using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260508000000_AddIsUpToDateToPullRequests")]
public sealed partial class AddIsUpToDateToPullRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsUpToDate",
            table: "PullRequests",
            type: "INTEGER",
            nullable: true,
            defaultValue: null
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsUpToDate", table: "PullRequests");
    }
}
