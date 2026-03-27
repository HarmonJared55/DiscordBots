using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommendBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarRoleToCommend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WarRole",
                table: "Commends",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarRole",
                table: "Commends");
        }
    }
}
