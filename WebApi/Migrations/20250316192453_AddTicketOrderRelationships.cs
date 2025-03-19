using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketOrderRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TicketOrderId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PresentationId",
                table: "SeatLocks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketOrderId",
                table: "SeatLocks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketOrderId",
                table: "Tickets",
                column: "TicketOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatLocks_TicketOrderId",
                table: "SeatLocks",
                column: "TicketOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_SeatLocks_TicketOrders_TicketOrderId",
                table: "SeatLocks",
                column: "TicketOrderId",
                principalTable: "TicketOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketOrders_TicketOrderId",
                table: "Tickets",
                column: "TicketOrderId",
                principalTable: "TicketOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeatLocks_TicketOrders_TicketOrderId",
                table: "SeatLocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketOrders_TicketOrderId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TicketOrderId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_SeatLocks_TicketOrderId",
                table: "SeatLocks");

            migrationBuilder.DropColumn(
                name: "TicketOrderId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PresentationId",
                table: "SeatLocks");

            migrationBuilder.DropColumn(
                name: "TicketOrderId",
                table: "SeatLocks");
        }
    }
}
