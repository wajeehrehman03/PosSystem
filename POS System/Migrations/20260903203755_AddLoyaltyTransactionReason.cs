using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosWebApi.POSSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyTransactionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "LoyaltyTransactions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "LoyaltyTransactions");
        }
    }
}
