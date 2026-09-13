using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosWebApi.POSSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderClientTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientTransactionId",
                table: "Orders",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientTransactionId",
                table: "Orders",
                column: "ClientTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ClientTransactionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClientTransactionId",
                table: "Orders");
        }
    }
}
