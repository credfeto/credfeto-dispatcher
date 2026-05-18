using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[Migration("20260518000000_AddAssigneeAndAiWorkFlagsToIssues")]
public sealed partial class AddAssigneeAndAiWorkFlagsToIssues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasAssignee",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            name: "IsAiWork",
            table: "Issues",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HasAssignee", table: "Issues");
        migrationBuilder.DropColumn(name: "IsAiWork", table: "Issues");
    }
}
