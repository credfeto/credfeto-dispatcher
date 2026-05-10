using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260510000001_RefineWorkItemFields")]
public sealed partial class RefineWorkItemFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FailedCheckSha",
            table: "PullRequests",
            type: "TEXT",
            nullable: true,
            defaultValue: null
        );

        migrationBuilder.AddColumn<int>(
            name: "LinkedPrNumber",
            table: "Issues",
            type: "INTEGER",
            nullable: true,
            defaultValue: null
        );

        migrationBuilder.Sql(
            "UPDATE Issues SET LinkedPrNumber = CASE WHEN HasLinkedPr = 1 THEN -1 ELSE NULL END"
        );

        // SQLite does not support DROP COLUMN via ALTER TABLE; recreate the table without HasLinkedPr.
        migrationBuilder.Sql(
            @"CREATE TABLE Issues_new (
    Repository TEXT NOT NULL,
    Id INTEGER NOT NULL,
    Status TEXT NOT NULL,
    FirstSeen TEXT NOT NULL,
    LastUpdated TEXT NOT NULL,
    WhenClosed TEXT,
    Priority INTEGER NOT NULL DEFAULT 0,
    IsOnHold INTEGER NOT NULL DEFAULT 0,
    LinkedPrNumber INTEGER,
    PRIMARY KEY (Repository, Id)
);
INSERT INTO Issues_new SELECT Repository, Id, Status, FirstSeen, LastUpdated, WhenClosed, Priority, IsOnHold, LinkedPrNumber FROM Issues;
DROP TABLE Issues;
ALTER TABLE Issues_new RENAME TO Issues;"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasLinkedPr",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.Sql(
            "UPDATE Issues SET HasLinkedPr = CASE WHEN LinkedPrNumber IS NOT NULL THEN 1 ELSE 0 END"
        );

        migrationBuilder.DropColumn(name: "LinkedPrNumber", table: "Issues");

        migrationBuilder.DropColumn(name: "FailedCheckSha", table: "PullRequests");
    }
}
