using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260424100000_AddOnHoldColumn")]
[SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Two tables with identical column schemas — refactoring is not applicable to EF Core migration structure.")]
public sealed partial class AddOnHoldColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "OnHold",
            table: "PullRequests",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "OnHold",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OnHold",
            table: "PullRequests");

        migrationBuilder.DropColumn(
            name: "OnHold",
            table: "Issues");
    }
}

