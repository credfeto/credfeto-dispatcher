using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260428000000_AddPriorityAndStatusFieldsToIssuesAndPullRequests")]
[SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Up/Down methods necessarily mirror each other; structurally identical ALTER TABLE statements for two tables.")]
public sealed partial class AddPriorityAndStatusFieldsToIssuesAndPullRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "PullRequests",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "IsOnHold",
            table: "PullRequests",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "IsOnHold",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "HasLinkedPr",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Priority", table: "PullRequests");
        migrationBuilder.DropColumn(name: "IsOnHold", table: "PullRequests");
        migrationBuilder.DropColumn(name: "Priority", table: "Issues");
        migrationBuilder.DropColumn(name: "IsOnHold", table: "Issues");
        migrationBuilder.DropColumn(name: "HasLinkedPr", table: "Issues");
    }
}
