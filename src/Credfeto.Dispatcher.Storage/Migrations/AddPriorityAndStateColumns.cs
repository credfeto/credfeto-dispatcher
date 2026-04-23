using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260424000000_AddPriorityAndStateColumns")]
[SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Two tables with identical column schemas — refactoring is not applicable to EF Core migration structure.")]
public sealed partial class AddPriorityAndStateColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Priority",
            table: "PullRequests",
            type: "TEXT",
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "PullRequests",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Priority",
            table: "Issues",
            type: "TEXT",
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "Issues",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Priority",
            table: "PullRequests");

        migrationBuilder.DropColumn(
            name: "State",
            table: "PullRequests");

        migrationBuilder.DropColumn(
            name: "Priority",
            table: "Issues");

        migrationBuilder.DropColumn(
            name: "State",
            table: "Issues");
    }
}

