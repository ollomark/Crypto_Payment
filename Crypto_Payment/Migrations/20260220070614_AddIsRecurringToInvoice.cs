using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crypto_Payment.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRecurringToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Mevcut otomatik faturalar: OrderNumber 'AUTO-' ile başlayanlar
            migrationBuilder.Sql(
                "UPDATE \"Invoices\" SET \"IsRecurring\" = 1 WHERE \"OrderNumber\" LIKE 'AUTO-%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Invoices");
        }
    }
}
