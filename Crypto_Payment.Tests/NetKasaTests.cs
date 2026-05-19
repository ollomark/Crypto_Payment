using Crypto_Payment.Data;
using Crypto_Payment.Manager;
using Crypto_Payment.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Crypto_Payment.Tests;

public class NetKasaTests
{
    private AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    // Senaryo: Net Kasa = Onaylı Gelirler - Onaylı Giderler
    [Fact]
    public async Task NetKasa_ShouldEqual_PaidIncome_Minus_ApprovedExpenses()
    {
        using var db = CreateInMemoryDb("test_net_kasa");
        var expenseManager = new ExpenseManager(db);

        // Onaylı faturalar (gelir)
        db.Invoices.Add(new Invoice
        {
            OrderName = "Fatura 1", OrderNumber = "ORD-001", Email = "a@a.com",
            SourceAmount = 500m, SourceCurrency = "USD", Currency = "USDT_TRX",
            Status = "completed", RegistrationStatus = true, CreatedDate = DateTime.UtcNow
        });
        db.Invoices.Add(new Invoice
        {
            OrderName = "Fatura 2", OrderNumber = "ORD-002", Email = "b@b.com",
            SourceAmount = 300m, SourceCurrency = "USD", Currency = "USDT_TRX",
            Status = "mismatch", RegistrationStatus = true, CreatedDate = DateTime.UtcNow
        });
        // Bekleyen fatura (gelire sayılmamalı)
        db.Invoices.Add(new Invoice
        {
            OrderName = "Fatura 3", OrderNumber = "ORD-003", Email = "c@c.com",
            SourceAmount = 200m, SourceCurrency = "USD", Currency = "USDT_TRX",
            Status = "pending", RegistrationStatus = true, CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Onaylı giderler
        await expenseManager.CreateAsync(new Expense
        {
            Amount = 100m, Currency = "USD", Category = ExpenseCategory.Maas,
            RequesterName = "Test", Description = "Maaş", Method = "Banka",
            Status = "Approved", CreatedDate = DateTime.UtcNow
        });
        await expenseManager.CreateAsync(new Expense
        {
            Amount = 50m, Currency = "USD", Category = ExpenseCategory.SunucuGideri,
            RequesterName = "Test", Description = "Sunucu", Method = "Kripto",
            Status = "Approved", CreatedDate = DateTime.UtcNow
        });
        // Bekleyen gider (Net Kasa'ya dahil edilmemeli)
        await expenseManager.CreateAsync(new Expense
        {
            Amount = 999m, Currency = "USD", Category = ExpenseCategory.KarCekimi,
            RequesterName = "Test", Description = "Bekleyen", Method = "Banka",
            Status = "Pending", CreatedDate = DateTime.UtcNow
        });

        // Hesapla
        var totalApprovedExpenses = await expenseManager.GetTotalApprovedAsync();
        var totalPaidIncome = await db.Invoices
            .Where(i => i.RegistrationStatus && (i.Status == "completed" || i.Status == "mismatch"))
            .SumAsync(i => (decimal?)i.SourceAmount) ?? 0m;

        var netKasa = totalPaidIncome - totalApprovedExpenses;

        // Beklenen: 500 + 300 = 800 gelir, 100 + 50 = 150 gider → Net Kasa = 650
        Assert.Equal(800m, totalPaidIncome);
        Assert.Equal(150m, totalApprovedExpenses);
        Assert.Equal(650m, netKasa);
    }

    // Senaryo: Gider onaylandığında GetTotalApprovedAsync artmalı
    [Fact]
    public async Task ApproveExpense_ShouldIncreaseApprovedTotal()
    {
        using var db = CreateInMemoryDb("test_expense_approve");
        var expenseManager = new ExpenseManager(db);

        var expense = await expenseManager.CreateAsync(new Expense
        {
            Amount = 250m, Currency = "USD", Category = ExpenseCategory.Avans,
            RequesterName = "Test User", Description = "Avans talebi", Method = "Banka",
            Status = "Pending", CreatedDate = DateTime.UtcNow
        });

        // Onaylamadan önce toplam 0 olmalı
        var beforeApproval = await expenseManager.GetTotalApprovedAsync();
        Assert.Equal(0m, beforeApproval);

        // Onayla
        await expenseManager.ApproveAsync(expense.Id, "masteradmin");

        // Onaylandıktan sonra toplam 250 olmalı
        var afterApproval = await expenseManager.GetTotalApprovedAsync();
        Assert.Equal(250m, afterApproval);

        // Durum "Approved" olmalı
        var updated = await expenseManager.GetByIdAsync(expense.Id);
        Assert.Equal("Approved", updated!.Status);
        Assert.Equal("masteradmin", updated.ReviewedBy);
    }
}
