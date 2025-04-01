using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDeleteConstraintToPaymentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_TicketOrders_TicketOrderId",
                table: "Payments");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_TicketOrders_TicketOrderId",
                table: "Payments",
                column: "TicketOrderId",
                principalTable: "TicketOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_TicketOrders_TicketOrderId",
                table: "Payments");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_TicketOrders_TicketOrderId",
                table: "Payments",
                column: "TicketOrderId",
                principalTable: "TicketOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
