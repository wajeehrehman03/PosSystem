using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosWebApi.POSSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCachingAndIndexingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_StockQuantity_MinimumStockThreshold",
                table: "Products",
                columns: new[] { "StockQuantity", "MinimumStockThreshold" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders",
                column: "OrderDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Products_StockQuantity_MinimumStockThreshold",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders");
        }
    }
}
