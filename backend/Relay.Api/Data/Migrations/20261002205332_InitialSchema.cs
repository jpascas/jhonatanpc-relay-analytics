using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(120)", nullable: false),
                    industry = table.Column<string>(type: "varchar(60)", nullable: false),
                    timezone = table.Column<string>(type: "varchar(60)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    location = table.Column<string>(type: "varchar(80)", nullable: false),
                    event_type = table.Column<string>(type: "varchar(40)", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    duration_seconds = table.Column<int>(type: "int", nullable: true),
                    outcome = table.Column<string>(type: "varchar(40)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_events_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_account_id",
                table: "activity_events",
                column: "account_id");

            // D8: exact duplicates (identical except id) count once. GROUP BY puts NULLs in one group,
            // matching the evidence query G-03 (12,626 raw rows -> 12,614).
            migrationBuilder.Sql(@"
CREATE VIEW activity_events_dedup AS
SELECT account_id, location, event_type, occurred_at, duration_seconds, outcome
FROM activity_events
GROUP BY account_id, location, event_type, occurred_at, duration_seconds, outcome;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW activity_events_dedup;");

            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
