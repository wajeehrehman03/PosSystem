using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosWebApi.POSSystem.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedDataDeterminism : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "$2a$11$.tIx7BZ9jxcDSMwpxwS36up/C7fMHTXG3AbOTgyqo.H9uLqYi8ci." });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "$2a$11$tpJJadQTA.kaP0FgNitXyuNxPGy6X8lLdRr46/nHJqza6SKevrQvS" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 9, 2, 20, 12, 45, 64, DateTimeKind.Utc).AddTicks(7625), "$2a$11$4z2VPKjURsiePIwo2zotreX5vAaz9On.VEYMDHzM33dxGbciMOe0." });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 9, 2, 20, 12, 45, 64, DateTimeKind.Utc).AddTicks(7631), "$2a$11$V4t7x7RgOzSUYQQATz9cH.nKaYSKvYvK7aD.osiuVWGYpsQy8govW" });
        }
    }
}
