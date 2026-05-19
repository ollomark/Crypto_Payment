using Crypto_Payment.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<SystemLog> SystemLogs { get; set; }
    public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
    public DbSet<PaymentLink> PaymentLinks { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<CustomerRefund> CustomerRefunds { get; set; }
    public DbSet<CustomerCollection> CustomerCollections { get; set; }
    public DbSet<StaffPayment> StaffPayments { get; set; }
    public DbSet<StaffProfile> StaffProfiles { get; set; }
    public DbSet<ApiClient> ApiClients { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Provider-agnostic decimal yapılandırması
        modelBuilder.Entity<InvoiceItem>()
            .Property(i => i.Price)
            .HasPrecision(18, 2);

        // Customer soft delete global query filter
        modelBuilder.Entity<Customer>()
            .HasQueryFilter(c => c.IsActive);

        // InvoiceItem.Total precision
        modelBuilder.Entity<InvoiceItem>()
            .Property(i => i.Total)
            .HasPrecision(18, 2);

        // Invoice.SourceAmount precision
        modelBuilder.Entity<Invoice>()
            .Property(i => i.SourceAmount)
            .HasPrecision(18, 2);

        // Invoice index'leri
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.TxnId)
            .IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.Status);

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.OrderNumber);

        // Invoice soft-delete global query filter
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => i.RegistrationStatus);

        // Invoice indexes
        modelBuilder.Entity<Invoice>().HasIndex(i => i.CustomerId);
        modelBuilder.Entity<Invoice>().HasIndex(i => i.RegistrationStatus);
        modelBuilder.Entity<Invoice>().HasIndex(i => i.CreatedDate);

        // Customer email unique index (SQLite allows multiple NULLs in unique indexes)
        modelBuilder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();

        // Invoice recurring index
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.IsRecurring);

        // PaymentLink
        modelBuilder.Entity<PaymentLink>()
            .HasOne(p => p.Invoice)
            .WithMany(i => i.PaymentLinks)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaymentLink>()
            .HasIndex(p => p.TxnId);

        modelBuilder.Entity<PaymentLink>()
            .HasIndex(p => p.InvoiceId);

        // Permission self-referencing FK
        modelBuilder.Entity<Permission>()
            .HasOne(p => p.TopPermission)
            .WithMany()
            .HasForeignKey(p => p.TopPermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Expense decimal precision
        modelBuilder.Entity<Expense>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

        // ApprovalRequest index
        modelBuilder.Entity<ApprovalRequest>()
            .HasIndex(a => a.Status);

        modelBuilder.Entity<ApprovalRequest>()
            .HasIndex(a => a.CreatedDate);

        // SystemLog index
        modelBuilder.Entity<SystemLog>()
            .HasIndex(s => s.CreatedDate);

        // WithdrawalRequest
        modelBuilder.Entity<WithdrawalRequest>()
            .HasIndex(w => w.Token)
            .IsUnique();

        modelBuilder.Entity<WithdrawalRequest>()
            .Property(w => w.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<WithdrawalRequest>()
            .HasIndex(w => w.Status);

        // PaymentMethod
        modelBuilder.Entity<PaymentMethod>()
            .HasIndex(pm => pm.Type);

        // ChatMessage indexes
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.SenderId, m.ReceiverId });
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => m.CreatedDate);

        modelBuilder.Entity<PaymentLink>()
            .HasOne(p => p.PaymentMethod)
            .WithMany()
            .HasForeignKey(p => p.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PaymentLink>()
            .Property(p => p.ExpectedAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PaymentLink>()
            .Property(p => p.ReceivedAmount)
            .HasPrecision(18, 2);

        // CustomerRefund
        modelBuilder.Entity<CustomerRefund>()
            .HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CustomerRefund>()
            .Property(r => r.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<CustomerRefund>()
            .HasIndex(r => r.CustomerId);

        // Expense.CustomerId
        modelBuilder.Entity<Expense>()
            .HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Expense>()
            .HasIndex(e => e.CustomerId);

        // CustomerCollection (manuel tahsilat)
        modelBuilder.Entity<CustomerCollection>()
            .HasOne(c => c.Customer)
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CustomerCollection>()
            .Property(c => c.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<CustomerCollection>()
            .HasIndex(c => c.CustomerId);

        // StaffProfile (maaş tutarı / ödeme günü)
        modelBuilder.Entity<StaffProfile>()
            .HasKey(s => s.UserId);
        modelBuilder.Entity<StaffProfile>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StaffProfile>()
            .Property(s => s.MonthlySalary)
            .HasPrecision(18, 2);

        // StaffPayment (personel avans/maaş)
        modelBuilder.Entity<StaffPayment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StaffPayment>()
            .HasOne(p => p.LinkedExpense)
            .WithMany()
            .HasForeignKey(p => p.ExpenseId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<StaffPayment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<StaffPayment>()
            .HasIndex(p => p.UserId);
        modelBuilder.Entity<StaffPayment>()
            .HasIndex(p => p.ExpenseId);
        modelBuilder.Entity<StaffPayment>()
            .HasIndex(p => new { p.PeriodYear, p.PeriodMonth });

        modelBuilder.Entity<ApiClient>()
            .HasIndex(a => a.ApiKeyHash)
            .IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.ApiClient)
            .WithMany()
            .HasForeignKey(i => i.ApiClientId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.ApiClientId, i.OrderNumber });
    }
}