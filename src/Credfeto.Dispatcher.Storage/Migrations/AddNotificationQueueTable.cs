using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260424000000_AddNotificationQueueTable")]
public sealed partial class AddNotificationQueueTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationQueue",
            columns: table => new
            {
                SubjectUrl = table.Column<string>(type: "TEXT", nullable: false),
                NotificationId = table.Column<string>(type: "TEXT", nullable: false),
                Repository = table.Column<string>(type: "TEXT", nullable: false),
                RepositoryUrl = table.Column<string>(type: "TEXT", nullable: false),
                SubjectType = table.Column<string>(type: "TEXT", nullable: false),
                SubjectTitle = table.Column<string>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                QueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                DispatchAfter = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationQueue", x => x.SubjectUrl);
            });

        migrationBuilder.CreateIndex(
            name: "IX_NotificationQueue_DispatchAfter",
            table: "NotificationQueue",
            column: "DispatchAfter");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("NotificationQueue");
    }
}
