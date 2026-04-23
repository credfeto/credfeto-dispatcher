using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[Migration("20260423100000_AddPullRequestAndIssueTables")]
[SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Two tables with identical column schemas — refactoring is not applicable to EF Core migration structure.")]
public sealed partial class AddPullRequestAndIssueTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PullRequests",
            columns: table => new
            {
                Repository = table.Column<string>(nullable: false),
                Id = table.Column<int>(nullable: false),
                Status = table.Column<string>(nullable: false),
                FirstSeen = table.Column<DateTimeOffset>(nullable: false),
                LastUpdated = table.Column<DateTimeOffset>(nullable: false),
                WhenClosed = table.Column<DateTimeOffset>(nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PullRequests", x => new { x.Repository, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Issues",
            columns: table => new
            {
                Repository = table.Column<string>(nullable: false),
                Id = table.Column<int>(nullable: false),
                Status = table.Column<string>(nullable: false),
                FirstSeen = table.Column<DateTimeOffset>(nullable: false),
                LastUpdated = table.Column<DateTimeOffset>(nullable: false),
                WhenClosed = table.Column<DateTimeOffset>(nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Issues", x => new { x.Repository, x.Id });
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PullRequests");
        migrationBuilder.DropTable("Issues");
    }
}
