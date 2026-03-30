using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClawSharp.Lib.Runtime;

[DbContext(typeof(ClawDbContext))]
[Migration("20260330010000_AddGlobalThreadSpace")]
internal sealed class AddGlobalThreadSpace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Rename is_init to is_global
        migrationBuilder.RenameColumn(
            name: "is_init",
            table: "thread_spaces",
            newName: "is_global");

        // Drop old index
        migrationBuilder.DropIndex(
            name: "IX_thread_spaces_bound_folder_path",
            table: "thread_spaces");

        // Create new filtered index
        migrationBuilder.CreateIndex(
            name: "IX_thread_spaces_bound_folder_path",
            table: "thread_spaces",
            column: "bound_folder_path",
            unique: true,
            filter: "bound_folder_path IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "is_global",
            table: "thread_spaces",
            newName: "is_init");

        migrationBuilder.DropIndex(
            name: "IX_thread_spaces_bound_folder_path",
            table: "thread_spaces");

        migrationBuilder.CreateIndex(
            name: "IX_thread_spaces_bound_folder_path",
            table: "thread_spaces",
            column: "bound_folder_path",
            unique: true);
    }
}