using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClawSharp.Lib.Runtime;

[DbContext(typeof(ClawDbContext))]
[Migration("20260330000000_InitialRuntimePersistence")]
internal sealed class InitialRuntimePersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "thread_spaces",
            columns: table => new
            {
                thread_space_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                bound_folder_path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),                
                is_init = table.Column<bool>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_thread_spaces", x => x.thread_space_id);
            });

        migrationBuilder.CreateTable(
            name: "sessions",
            columns: table => new
            {
                session_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                agent_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                thread_space_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                workspace_root = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                status = table.Column<int>(type: "INTEGER", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ended_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sessions", x => x.session_id);
                table.ForeignKey(
                    name: "FK_sessions_thread_spaces_thread_space_id",
                    column: x => x.thread_space_id,
                    principalTable: "thread_spaces",
                    principalColumn: "thread_space_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "messages",
            columns: table => new
            {
                message_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                session_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                turn_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                role = table.Column<int>(type: "INTEGER", nullable: false),
                content = table.Column<string>(type: "TEXT", maxLength: 10000000, nullable: false),
                name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                tool_call_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                blocks_json = table.Column<string>(type: "TEXT", maxLength: 10000000, nullable: true),
                sequence_no = table.Column<int>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_messages", x => x.message_id);
                table.ForeignKey(
                    name: "FK_messages_sessions_session_id",
                    column: x => x.session_id,
                    principalTable: "sessions",
                    principalColumn: "session_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "session_events",
            columns: table => new
            {
                event_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                session_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                turn_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                event_type = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                payload = table.Column<string>(type: "TEXT", maxLength: 10000000, nullable: false),
                sequence_no = table.Column<int>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_session_events", x => x.event_id);
                table.ForeignKey(
                    name: "FK_session_events_sessions_session_id",
                    column: x => x.session_id,
                    principalTable: "sessions",
                    principalColumn: "session_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_messages_session_id_sequence_no",
            table: "messages",
            columns: ["session_id", "sequence_no"]);

        migrationBuilder.CreateIndex(
            name: "IX_session_events_session_id_sequence_no",
            table: "session_events",
            columns: ["session_id", "sequence_no"]);

        migrationBuilder.CreateIndex(
            name: "IX_sessions_thread_space_id",
            table: "sessions",
            column: "thread_space_id");

        migrationBuilder.CreateIndex(
            name: "IX_thread_spaces_bound_folder_path",
            table: "thread_spaces",
            column: "bound_folder_path",
            unique: true,
            filter: "bound_folder_path IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_thread_spaces_name",
            table: "thread_spaces",
            column: "name",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "messages");
        migrationBuilder.DropTable(name: "session_events");
        migrationBuilder.DropTable(name: "sessions");
        migrationBuilder.DropTable(name: "thread_spaces");
    }
}
