using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260510000000_AddPullRequestMetricsFields")]
public sealed partial class AddPullRequestMetricsFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CommentCount",
            table: "PullRequests",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<string>(
            name: "ReviewDecision",
            table: "PullRequests",
            type: "TEXT",
            nullable: true,
            defaultValue: null
        );

        migrationBuilder.AddColumn<int>(
            name: "FailedCheckCount",
            table: "PullRequests",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<string>(
            name: "FailedCheckNames",
            table: "PullRequests",
            type: "TEXT",
            nullable: true,
            defaultValue: null
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CommentCount", table: "PullRequests");
        migrationBuilder.DropColumn(name: "ReviewDecision", table: "PullRequests");
        migrationBuilder.DropColumn(name: "FailedCheckCount", table: "PullRequests");
        migrationBuilder.DropColumn(name: "FailedCheckNames", table: "PullRequests");
    }
}
