using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[SuppressMessage(
    "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
    "PH2071:Duplicate shape found",
    Justification = "Issues and PullRequests tables share structural property patterns by domain design; refactoring migration schema definitions is not applicable."
)]
public sealed partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateIssuesTable(migrationBuilder);
        CreateNotificationQueueTable(migrationBuilder);
        CreatePollingStatesTable(migrationBuilder);
        CreatePullRequestsTable(migrationBuilder);
        CreateReposTable(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Issues");
        migrationBuilder.DropTable(name: "NotificationQueue");
        migrationBuilder.DropTable(name: "PollingStates");
        migrationBuilder.DropTable(name: "PullRequests");
        migrationBuilder.DropTable(name: "Repos");
    }

    [UnconditionalSuppressMessage(
        category: "Trimming",
        checkId: "IL2026",
        Justification = "CreateTable column lambda compiles to Expression.New which has RequiresUnreferencedCode; migration assembly is rooted by TrimmerRootAssembly so all members are preserved."
    )]
    private static void CreateIssuesTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Issues",
            columns: table => new
            {
                Repository = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Id = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                FirstSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                WhenClosed = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Priority = table.Column<int>(type: "int", nullable: false),
                IsOnHold = table.Column<bool>(type: "bit", nullable: false),
                LinkedPrNumber = table.Column<int>(type: "int", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Issues", x => new { x.Repository, x.Id });
            }
        );
    }

    private static void CreateNotificationQueueTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationQueue",
            columns: table => new
            {
                SubjectUrl = table.Column<string>(type: "nvarchar(450)", nullable: false),
                NotificationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Repository = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RepositoryUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SubjectType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SubjectTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DispatchAfter = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationQueue", x => x.SubjectUrl);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_NotificationQueue_DispatchAfter",
            table: "NotificationQueue",
            column: "DispatchAfter"
        );
    }

    private static void CreatePollingStatesTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PollingStates",
            columns: table => new
            {
                Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ETag = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PollingStates", x => x.Key);
            }
        );
    }

    [UnconditionalSuppressMessage(
        category: "Trimming",
        checkId: "IL2026",
        Justification = "CreateTable column lambda compiles to Expression.New which has RequiresUnreferencedCode; migration assembly is rooted by TrimmerRootAssembly so all members are preserved."
    )]
    private static void CreatePullRequestsTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PullRequests",
            columns: table => new
            {
                Repository = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Id = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                FirstSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                WhenClosed = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Priority = table.Column<int>(type: "int", nullable: false),
                IsOnHold = table.Column<bool>(type: "bit", nullable: false),
                CommentCount = table.Column<int>(type: "int", nullable: false),
                ReviewDecision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FailedCheckCount = table.Column<int>(type: "int", nullable: false),
                FailedCheckNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FailedCheckSha = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PullRequests", x => new { x.Repository, x.Id });
            }
        );
    }

    private static void CreateReposTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Repos",
            columns: table => new
            {
                Repository = table.Column<string>(type: "nvarchar(450)", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Repos", x => x.Repository);
            }
        );
    }
}
