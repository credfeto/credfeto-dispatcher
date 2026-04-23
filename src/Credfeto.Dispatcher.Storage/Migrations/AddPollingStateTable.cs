using Microsoft.EntityFrameworkCore.Migrations;

namespace Credfeto.Dispatcher.Storage.Migrations;

[Migration("20260423120000_AddPollingStateTable")]
public sealed partial class AddPollingStateTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PollingStates",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ETag = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PollingStates", x => x.Key)
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PollingStates");
    }
}
