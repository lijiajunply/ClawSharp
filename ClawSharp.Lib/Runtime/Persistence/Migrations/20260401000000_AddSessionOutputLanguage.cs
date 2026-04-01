using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClawSharp.Lib.Runtime
{
    /// <inheritdoc />
    [DbContext(typeof(ClawDbContext))]
    [Migration("20260401000000_AddSessionOutputLanguage")]
    public partial class AddSessionOutputLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "output_language_override",
                table: "sessions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "output_language_override",
                table: "sessions");
        }
    }
}
