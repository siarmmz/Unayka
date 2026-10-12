using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unayka.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrdersPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "CartItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "CartItems");
        }
    }
}
