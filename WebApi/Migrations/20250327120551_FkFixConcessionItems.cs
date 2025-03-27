using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class FkFixConcessionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderConcessionItems_OrderConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderConcessionItems_ConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems",
                column: "ConcessionItemId",
                principalTable: "ConcessionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderConcessionItems_ConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderConcessionItems_OrderConcessionItems_ConcessionItemId",
                table: "OrderConcessionItems",
                column: "ConcessionItemId",
                principalTable: "OrderConcessionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
