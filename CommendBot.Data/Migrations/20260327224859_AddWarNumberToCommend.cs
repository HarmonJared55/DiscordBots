using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommendBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarNumberToCommend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarNumber",
                table: "Commends",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarNumber",
                table: "Commends");
        }
    }
}
