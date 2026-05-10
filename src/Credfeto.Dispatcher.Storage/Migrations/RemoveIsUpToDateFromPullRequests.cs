using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260510000002_RemoveIsUpToDateFromPullRequests")]
public sealed partial class RemoveIsUpToDateFromPullRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            @"
CREATE TABLE PullRequests_new (
    Repository TEXT NOT NULL,
    Id INTEGER NOT NULL,
    Status TEXT NOT NULL,
    FirstSeen TEXT NOT NULL,
    LastUpdated TEXT NOT NULL,
    WhenClosed TEXT,
    Priority INTEGER NOT NULL DEFAULT 0,
    IsOnHold INTEGER NOT NULL DEFAULT 0,
    CommentCount INTEGER NOT NULL DEFAULT 0,
    ReviewDecision TEXT,
    FailedCheckCount INTEGER NOT NULL DEFAULT 0,
    FailedCheckNames TEXT,
    FailedCheckSha TEXT,
    PRIMARY KEY (Repository, Id)
);
INSERT INTO PullRequests_new
    SELECT Repository, Id, Status, FirstSeen, LastUpdated, WhenClosed,
           Priority, IsOnHold, CommentCount, ReviewDecision,
           FailedCheckCount, FailedCheckNames, FailedCheckSha
    FROM PullRequests;
DROP TABLE PullRequests;
ALTER TABLE PullRequests_new RENAME TO PullRequests;"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsUpToDate",
            table: "PullRequests",
            type: "INTEGER",
            nullable: true
        );
    }
}
