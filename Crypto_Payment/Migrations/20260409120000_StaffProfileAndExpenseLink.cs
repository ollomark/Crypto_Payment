using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crypto_Payment.Migrations;

public partial class StaffProfileAndExpenseLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StaffProfiles",
            columns: table => new
            {
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                MonthlySalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                SalaryDayOfMonth = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StaffProfiles", x => x.UserId);
                table.ForeignKey(
                    name: "FK_StaffProfiles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<int>(
            name: "ExpenseId",
            table: "StaffPayments",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_StaffPayments_ExpenseId",
            table: "StaffPayments",
            column: "ExpenseId");

        migrationBuilder.AddForeignKey(
            name: "FK_StaffPayments_Expenses_ExpenseId",
            table: "StaffPayments",
            column: "ExpenseId",
            principalTable: "Expenses",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_StaffPayments_Expenses_ExpenseId",
            table: "StaffPayments");

        migrationBuilder.DropIndex(
            name: "IX_StaffPayments_ExpenseId",
            table: "StaffPayments");

        migrationBuilder.DropColumn(
            name: "ExpenseId",
            table: "StaffPayments");

        migrationBuilder.DropTable(
            name: "StaffProfiles");
    }
}
