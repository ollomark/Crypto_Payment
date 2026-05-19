using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crypto_Payment.Migrations
{
    /// <inheritdoc />
    public partial class SecurityAndDbImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TopPermissionId",
                table: "Permissions",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // TopPermissionId = 0 olan kayıtları NULL'a çevir (FK constraint ihlalini önlemek için)
            migrationBuilder.Sql("UPDATE \"Permissions\" SET \"TopPermissionId\" = NULL WHERE \"TopPermissionId\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TopPermissionId",
                table: "Permissions",
                column: "TopPermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrderNumber",
                table: "Invoices",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TxnId",
                table: "Invoices",
                column: "TxnId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Permissions_TopPermissionId",
                table: "Permissions",
                column: "TopPermissionId",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Permissions_TopPermissionId",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_TopPermissionId",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrderNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Status",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TxnId",
                table: "Invoices");

            migrationBuilder.AlterColumn<int>(
                name: "TopPermissionId",
                table: "Permissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
