using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessionItemsFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrderConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems",
                column: "ConcessionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderConcessionItems_OrderId",
                table: "OrderConcessionItems",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderConcessionItems_OrderConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems",
                column: "ConcessionItemId",
                principalTable: "OrderConcessionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderConcessionItems_TicketOrders_OrderId",
                table: "OrderConcessionItems",
                column: "OrderId",
                principalTable: "TicketOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderConcessionItems_OrderConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderConcessionItems_TicketOrders_OrderId",
                table: "OrderConcessionItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderConcessionItems_OrderId",
                table: "OrderConcessionItems");
        }
    }
}
