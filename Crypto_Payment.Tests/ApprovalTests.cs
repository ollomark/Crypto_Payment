using Crypto_Payment.Data;
using Crypto_Payment.Manager;
using Crypto_Payment.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Crypto_Payment.Tests;

public class ApprovalTests
{
    private AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    // Senaryo 1: Standart Admin fatura silme talebi oluşturduğunda
    // fatura veritabanından silinmemeli, ApprovalRequests'e düşmeli
    [Fact]
    public async Task InvoiceDeleteRequest_ShouldCreateApprovalRequest_NotDeleteInvoice()
    {
        using var db = CreateInMemoryDb("test_delete_approval");
        var approvalManager = new ApprovalManager(db);

        // Fatura oluştur
        var invoice = new Invoice
        {
            OrderName = "Test Fatura",
            OrderNumber = "ORD-001",
            Email = "test@test.com",
            SourceAmount = 100m,
            SourceCurrency = "USD",
            Currency = "USDT_TRX",
            Status = "pending",
            RegistrationStatus = true,
            CreatedDate = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        // Standart admin silme talebi oluşturur (direkt silmez)
        var req = new ApprovalRequest
        {
            RequestType = "InvoiceDelete",
            RequestData = System.Text.Json.JsonSerializer.Serialize(new { InvoiceId = invoice.Id }),
            RequestedBy = "user-123",
            RequestedByName = "Standart Admin",
            Description = $"Fatura #{invoice.Id} silme talebi",
            Status = "Pending"
        };
        await approvalManager.CreateAsync(req);

        // Fatura hala veritabanında olmalı
        var invoiceInDb = await db.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == invoice.Id);
        Assert.NotNull(invoiceInDb);
        Assert.True(invoiceInDb.RegistrationStatus); // Silinmemiş

        // ApprovalRequest oluşturulmuş olmalı
        var pendingCount = await approvalManager.GetPendingCountAsync();
        Assert.Equal(1, pendingCount);

        var pending = await approvalManager.GetPendingAsync();
        Assert.Single(pending);
        Assert.Equal("InvoiceDelete", pending[0].RequestType);
        Assert.Equal("Pending", pending[0].Status);
    }

    // Senaryo 2: MasterAdmin onay verdiğinde fatura silinmeli
    [Fact]
    public async Task MasterAdminApprove_ShouldMarkApprovalAsApproved()
    {
        using var db = CreateInMemoryDb("test_approve_flow");
        var approvalManager = new ApprovalManager(db);

        var req = new ApprovalRequest
        {
            RequestType = "InvoiceStatusChange",
            RequestData = System.Text.Json.JsonSerializer.Serialize(new { InvoiceId = 1, NewStatus = "completed" }),
            RequestedBy = "user-456",
            RequestedByName = "Standart Admin",
            Description = "Fatura #1 durum değişimi: pending → completed",
            Status = "Pending"
        };
        await approvalManager.CreateAsync(req);

        // MasterAdmin onaylar
        await approvalManager.ApproveAsync(req.Id, "masteradmin");

        var approved = await approvalManager.GetByIdAsync(req.Id);
        Assert.NotNull(approved);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("masteradmin", approved.ReviewedBy);
        Assert.NotNull(approved.ReviewedDate);
    }

    // Senaryo 3: Red işlemi doğru çalışmalı
    [Fact]
    public async Task MasterAdminReject_ShouldMarkApprovalAsRejected_WithNote()
    {
        using var db = CreateInMemoryDb("test_reject_flow");
        var approvalManager = new ApprovalManager(db);

        var req = new ApprovalRequest
        {
            RequestType = "InvoiceDelete",
            RequestData = "{}",
            RequestedBy = "user-789",
            RequestedByName = "Standart Admin",
            Description = "Test talebi",
            Status = "Pending"
        };
        await approvalManager.CreateAsync(req);

        await approvalManager.RejectAsync(req.Id, "masteradmin", "Geçersiz talep.");

        var rejected = await approvalManager.GetByIdAsync(req.Id);
        Assert.NotNull(rejected);
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal("Geçersiz talep.", rejected.AdminNote);
    }
}
