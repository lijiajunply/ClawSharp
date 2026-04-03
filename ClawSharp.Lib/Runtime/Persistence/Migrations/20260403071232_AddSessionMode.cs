using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClawSharp.Lib.Runtime
{
    /// <inheritdoc />
    public partial class AddSessionMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "sessions");
        }
    }
}
