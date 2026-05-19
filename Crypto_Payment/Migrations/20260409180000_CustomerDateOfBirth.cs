using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crypto_Payment.Migrations;

public partial class CustomerDateOfBirth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "DateOfBirth",
            table: "Customers",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DateOfBirth",
            table: "Customers");
    }
}
