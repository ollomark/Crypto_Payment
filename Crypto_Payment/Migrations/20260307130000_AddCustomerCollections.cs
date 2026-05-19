using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crypto_Payment.Migrations;

public partial class AddCustomerCollections : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.CreateTable(
            name: "CustomerCollections",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Reference = table.Column<string>(type: "TEXT", nullable: true),
                CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerCollections", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomerCollections_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
        mb.CreateIndex(name: "IX_CustomerCollections_CustomerId", table: "CustomerCollections", column: "CustomerId");
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.DropTable(name: "CustomerCollections");
    }
}
