using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[Migration("20260423000000_InitialCreate")]
public sealed partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No schema changes in the initial migration — tables are added in subsequent migrations
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No schema to revert in the initial migration
    }
}
