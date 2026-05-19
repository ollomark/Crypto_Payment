using System.Threading.RateLimiting;
using Crypto_Payment.Authentication;
using Crypto_Payment.Manager;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Crypto_Payment.Data;
using Crypto_Payment.Helpers;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        // Heroku PostgreSQL
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var csb = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Require
        };

        opt.UseNpgsql(csb.ConnectionString);
    }
    else
    {
        // Local SQLite
        opt.UseSqlite("Data Source=invoice.db");
    }
});

builder.Services
    .AddIdentity<User, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<PlisioOptions>(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("Plisio__ApiKey") ?? "";
    options.SecretKey = Environment.GetEnvironmentVariable("Plisio__SecretKey") ?? "";
    options.BaseUrl = "https://api.plisio.net";
});

builder.Services.AddHttpClient<IPlisioService, PlisioManager>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/api/auth/login";
    options.AccessDeniedPath = "/api/auth/login";
});

// SecurityStamp doğrulama: her 0 dakikada bir (her request'te) cookie yenilenir
// Bu sayede rol değişiklikleri anında yansır
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.Configure<SmtpSettings>(options =>
{
    var section = builder.Configuration.GetSection("Smtp");
    section.Bind(options);

    // Override with environment variables if present
    options.User = Environment.GetEnvironmentVariable("SMTP_USER") ?? options.User;
    options.Pass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? options.Pass;
    options.From = Environment.GetEnvironmentVariable("SMTP_FROM") ?? options.From;
});

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IMailingService, MailingService>();
builder.Services.AddScoped<ICustomerService, CustomerManager>();
builder.Services.AddScoped<IInvoiceService, InvoiceManager>();
builder.Services.AddScoped<IInvoiceItemService, InvoiceItemManager>();
builder.Services.AddScoped<IRoleService, AppRoleManager>();
builder.Services.AddScoped<IPermissionService, PermissionManager>();
builder.Services.AddScoped<IApprovalService, ApprovalManager>();
builder.Services.AddScoped<IExpenseService, ExpenseManager>();
builder.Services.AddScoped<IWithdrawalRequestService, WithdrawalRequestManager>();
builder.Services.AddScoped<IExternalPaymentService, ExternalPaymentService>();
builder.Services.AddSingleton<IExternalWebhookService, ExternalWebhookService>();
builder.Services.AddScoped<IPushNotificationService, NoOpPushNotificationService>();
builder.Services.AddHttpClient<ITelegramBotService, TelegramBotManager>();
builder.Services.AddHttpClient<ITronService, TronManager>();
builder.Services.AddHttpClient("ExternalWebhook", c => c.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.SchemeName, _ => { });
builder.Services.AddHostedService<InvoiceReminderWorker>();
builder.Services.AddHostedService<CryptoPaymentWorker>();
builder.Services.AddHostedService<PushNotificationWorker>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<SupportSessionTracker>();
builder.Services.AddSingleton<IR2StorageService, R2StorageManager>();

// Heroku PORT desteği
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("callback", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("external-api", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',')
            ?? new[] { "https://payment.wanda.to", "https://localhost:5001" };
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Heroku/proxy için: X-Forwarded-Proto, X-Forwarded-Host doğru işlensin (email doğrulama linki https olsun)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

logger.LogInformation("Database migration scope starting");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    logger.LogInformation("DB Provider: {Provider}", db.Database.ProviderName);

    try
    {
        logger.LogInformation("Database migration starting");
        if (db.Database.IsNpgsql())
        {
            // PostgreSQL (Heroku): Migration'lar SQLite ile oluşturulduğu için
            // PostgreSQL'de EnsureCreated kullanıyoruz
            db.Database.EnsureCreated();

            // Yeni tablolar için manuel DDL (EnsureCreated mevcut DB'yi değiştirmez)
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""RecurringInvoices"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""SourceInvoiceId"" INTEGER NULL,
                    ""CustomerId"" INTEGER NOT NULL,
                    ""OrderName"" TEXT NOT NULL DEFAULT '',
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""SourceCurrency"" TEXT NOT NULL DEFAULT 'USD',
                    ""Currency"" TEXT NOT NULL DEFAULT 'USDT_TRX',
                    ""CallbackUrl"" TEXT NOT NULL DEFAULT '',
                    ""DayOfMonth"" INTEGER NOT NULL DEFAULT 1,
                    ""TotalMonths"" INTEGER NOT NULL DEFAULT 12,
                    ""CompletedCount"" INTEGER NOT NULL DEFAULT 0,
                    ""ReminderDaysBefore"" INTEGER NOT NULL DEFAULT 3,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""StartDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""NextRunDate"" TIMESTAMP WITH TIME ZONE NULL,
                    ""LastRunDate"" TIMESTAMP WITH TIME ZONE NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""TelegramNotify"" BOOLEAN NOT NULL DEFAULT TRUE
                );
                CREATE INDEX IF NOT EXISTS ""IX_RecurringInvoices_IsActive"" ON ""RecurringInvoices"" (""IsActive"");
                CREATE INDEX IF NOT EXISTS ""IX_RecurringInvoices_NextRunDate"" ON ""RecurringInvoices"" (""NextRunDate"");

                CREATE TABLE IF NOT EXISTS ""ApprovalRequests"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""RequestType"" TEXT NOT NULL DEFAULT '',
                    ""RequestData"" TEXT NOT NULL DEFAULT '',
                    ""RequestedBy"" TEXT NOT NULL DEFAULT '',
                    ""RequestedByName"" TEXT NOT NULL DEFAULT '',
                    ""Status"" TEXT NOT NULL DEFAULT 'Pending',
                    ""AdminNote"" TEXT NULL,
                    ""ReviewedBy"" TEXT NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""ReviewedDate"" TIMESTAMP WITH TIME ZONE NULL,
                    ""Description"" TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS ""IX_ApprovalRequests_Status"" ON ""ApprovalRequests"" (""Status"");

                CREATE TABLE IF NOT EXISTS ""Expenses"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Currency"" TEXT NOT NULL DEFAULT 'USD',
                    ""Category"" INTEGER NOT NULL DEFAULT 6,
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""RequesterName"" TEXT NOT NULL DEFAULT '',
                    ""RequesterCompany"" TEXT NULL,
                    ""Method"" TEXT NOT NULL DEFAULT 'Banka',
                    ""PaymentNetwork"" TEXT NULL,
                    ""WalletAddress"" TEXT NULL,
                    ""BankInfo"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'Pending',
                    ""AdminNote"" TEXT NULL,
                    ""ReviewedBy"" TEXT NULL,
                    ""RequestedByIp"" TEXT NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""ReviewedDate"" TIMESTAMP WITH TIME ZONE NULL
                );

                CREATE TABLE IF NOT EXISTS ""SystemLogs"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Action"" TEXT NOT NULL DEFAULT '',
                    ""UserId"" TEXT NULL,
                    ""UserName"" TEXT NULL,
                    ""TargetEntity"" TEXT NULL,
                    ""TargetId"" TEXT NULL,
                    ""OldValue"" TEXT NULL,
                    ""NewValue"" TEXT NULL,
                    ""IpAddress"" TEXT NULL,
                    ""Details"" TEXT NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ""IX_SystemLogs_CreatedDate"" ON ""SystemLogs"" (""CreatedDate"");

                CREATE TABLE IF NOT EXISTS ""WithdrawalRequests"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Token"" UUID NOT NULL DEFAULT gen_random_uuid(),
                    ""CreatedByAdminId"" TEXT NOT NULL DEFAULT '',
                    ""CreatedByAdminName"" TEXT NOT NULL DEFAULT '',
                    ""CustomerName"" TEXT NULL,
                    ""CompanyName"" TEXT NULL,
                    ""Amount"" NUMERIC(18,2) NULL,
                    ""PaymentMethod"" INTEGER NULL,
                    ""CryptoNetwork"" INTEGER NULL,
                    ""WalletAddressOrIban"" TEXT NULL,
                    ""Status"" INTEGER NOT NULL DEFAULT 0,
                    ""RequestDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""FilledDate"" TIMESTAMP WITH TIME ZONE NULL,
                    ""ReviewedDate"" TIMESTAMP WITH TIME ZONE NULL,
                    ""ReviewedBy"" TEXT NULL,
                    ""AdminNote"" TEXT NULL,
                    ""FilledByIp"" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WithdrawalRequests_Token"" ON ""WithdrawalRequests"" (""Token"");
                CREATE INDEX IF NOT EXISTS ""IX_WithdrawalRequests_Status"" ON ""WithdrawalRequests"" (""Status"");

                -- v65: Yeni sütunlar
                ALTER TABLE ""WithdrawalRequests"" ADD COLUMN IF NOT EXISTS ""Category"" INTEGER NOT NULL DEFAULT 5;
                ALTER TABLE ""WithdrawalRequests"" ADD COLUMN IF NOT EXISTS ""RequestCode"" TEXT NOT NULL DEFAULT '';
                ALTER TABLE ""WithdrawalRequests"" ADD COLUMN IF NOT EXISTS ""CustomerNote"" TEXT NULL;

                -- v85: Blockchain TX ID sütunu
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""TransactionId"" TEXT NULL;

                -- v92: Çekim talebi para birimi
                ALTER TABLE ""WithdrawalRequests"" ADD COLUMN IF NOT EXISTS ""Currency"" TEXT NULL;

                -- v96: Otomatik ödeme talimatı
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""IsRecurring"" BOOLEAN NOT NULL DEFAULT FALSE;

                -- v100: Yinelenen fatura alanları
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""RecurringDay"" INTEGER NULL;
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""LastReminderDate"" TIMESTAMP WITH TIME ZONE NULL;

                -- v101: Ödeme linkleri tablosu
                CREATE TABLE IF NOT EXISTS ""PaymentLinks"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""InvoiceId"" INTEGER NOT NULL REFERENCES ""Invoices""(""Id"") ON DELETE CASCADE,
                    ""TxnId"" TEXT NULL,
                    ""InvoiceUrl"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'new',
                    ""TransactionId"" TEXT NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""ExpiredDate"" TIMESTAMP WITH TIME ZONE NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_PaymentLinks_InvoiceId"" ON ""PaymentLinks"" (""InvoiceId"");
                CREATE INDEX IF NOT EXISTS ""IX_PaymentLinks_TxnId"" ON ""PaymentLinks"" (""TxnId"");

                -- v114: Manuel ödeme alanları
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""Note"" TEXT NULL;
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""IsManual"" BOOLEAN NOT NULL DEFAULT FALSE;

                -- v129: Ödeme Yöntemleri tablosu
                CREATE TABLE IF NOT EXISTS ""PaymentMethods"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Name"" TEXT NOT NULL DEFAULT '',
                    ""Type"" TEXT NOT NULL DEFAULT 'manual',
                    ""Description"" TEXT NULL,
                    ""Icon"" TEXT NULL,
                    ""BankName"" TEXT NULL,
                    ""AccountHolder"" TEXT NULL,
                    ""Iban"" TEXT NULL,
                    ""WalletAddress"" TEXT NULL,
                    ""WalletNetwork"" TEXT NULL,
                    ""ApiKey"" TEXT NULL,
                    ""ExtraConfig"" TEXT NULL,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ""IX_PaymentMethods_Type"" ON ""PaymentMethods"" (""Type"");

                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""PaymentMethodId"" INTEGER NULL REFERENCES ""PaymentMethods""(""Id"") ON DELETE SET NULL;

                -- Seed: Plisio (varsayılan)
                INSERT INTO ""PaymentMethods"" (""Name"", ""Type"", ""Description"", ""Icon"", ""IsActive"", ""IsDefault"", ""SortOrder"")
                SELECT 'Plisio (Kripto)', 'plisio', 'Plisio üzerinden kripto para ile ödeme', 'ri-bit-coin-line', TRUE, TRUE, 1
                WHERE NOT EXISTS (SELECT 1 FROM ""PaymentMethods"" WHERE ""Type"" = 'plisio');

                INSERT INTO ""PaymentMethods"" (""Name"", ""Type"", ""Description"", ""Icon"", ""IsActive"", ""IsDefault"", ""SortOrder"")
                SELECT 'Banka Havalesi', 'bank_transfer', 'Havale/EFT ile ödeme', 'ri-bank-line', TRUE, FALSE, 2
                WHERE NOT EXISTS (SELECT 1 FROM ""PaymentMethods"" WHERE ""Type"" = 'bank_transfer');

                INSERT INTO ""PaymentMethods"" (""Name"", ""Type"", ""Description"", ""Icon"", ""IsActive"", ""IsDefault"", ""SortOrder"")
                SELECT 'Kredi Kartı', 'credit_card', 'Kredi kartı ile ödeme', 'ri-bank-card-line', FALSE, FALSE, 3
                WHERE NOT EXISTS (SELECT 1 FROM ""PaymentMethods"" WHERE ""Type"" = 'credit_card');

                INSERT INTO ""PaymentMethods"" (""Name"", ""Type"", ""Description"", ""Icon"", ""IsActive"", ""IsDefault"", ""SortOrder"")
                SELECT 'Manuel Ödeme', 'manual', 'Manuel ödeme kaydı', 'ri-hand-coin-line', TRUE, FALSE, 4
                WHERE NOT EXISTS (SELECT 1 FROM ""PaymentMethods"" WHERE ""Type"" = 'manual');

                -- v130: Hızlı Kripto - PaymentLink yeni alanları
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""CryptoWalletAddress"" TEXT NULL;
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""CryptoCurrency"" TEXT NULL;
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""ExpectedAmount"" NUMERIC(18,2) NULL;
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""ConfirmedTxHash"" TEXT NULL;
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""ReceivedAmount"" NUMERIC(18,2) NULL;

                -- v155: Manuel ödeme ay takibi
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""PaidForMonth"" INTEGER NULL;
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""PaidForYear"" INTEGER NULL;

                -- v171: Ödeme tarihi (kripto linklerde oluşturulma≠ödeme; gecikmiş listesi için)
                ALTER TABLE ""PaymentLinks"" ADD COLUMN IF NOT EXISTS ""PaidDate"" TIMESTAMP WITH TIME ZONE NULL;

                -- Mevcut manuel ödemelerde PaidForMonth/Year'ı CreatedDate'ten doldur
                UPDATE ""PaymentLinks""
                SET ""PaidForMonth"" = EXTRACT(MONTH FROM ""CreatedDate"")::INT,
                    ""PaidForYear""  = EXTRACT(YEAR  FROM ""CreatedDate"")::INT
                WHERE ""IsManual"" = TRUE AND ""PaidForMonth"" IS NULL;

                -- Eski manuel ödemelerin sahte CreatedDate'ini (tam gün başı: 00:00:00)
                -- gerçek bir zamana çeviriyoruz ki rapor filtresinde doğru düşsün.
                -- Saat kısmı 00:00:00 olanları tespit edip NOW() ile güncelliyoruz.
                UPDATE ""PaymentLinks""
                SET ""CreatedDate"" = NOW()
                WHERE ""IsManual"" = TRUE
                  AND ""PaidForMonth"" IS NOT NULL
                  AND EXTRACT(HOUR FROM ""CreatedDate"") = 0
                  AND EXTRACT(MINUTE FROM ""CreatedDate"") = 0
                  AND EXTRACT(SECOND FROM ""CreatedDate"") = 0;

                -- Seed: Hızlı Kripto ödeme yöntemi
                INSERT INTO ""PaymentMethods"" (""Name"", ""Type"", ""Description"", ""Icon"", ""IsActive"", ""IsDefault"", ""SortOrder"")
                SELECT 'Hızlı Kripto', 'fast_crypto', 'TRX ve USDT (TRC20) ile anlık kripto ödeme', 'ri-flashlight-line', TRUE, FALSE, 0
                WHERE NOT EXISTS (SELECT 1 FROM ""PaymentMethods"" WHERE ""Type"" = 'fast_crypto');

                -- v144: Chat mesajları tablosu
                CREATE TABLE IF NOT EXISTS ""ChatMessages"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""SenderId"" TEXT NOT NULL DEFAULT '',
                    ""ReceiverId"" TEXT NOT NULL DEFAULT '',
                    ""Content"" TEXT NULL,
                    ""FileUrl"" TEXT NULL,
                    ""FileName"" TEXT NULL,
                    ""FileSize"" BIGINT NULL,
                    ""IsRead"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS ""IX_ChatMessages_SenderId_ReceiverId"" ON ""ChatMessages"" (""SenderId"", ""ReceiverId"");
                CREATE INDEX IF NOT EXISTS ""IX_ChatMessages_CreatedDate"" ON ""ChatMessages"" (""CreatedDate"");

                -- v200: Müşteri cari hareketleri - Geri ödeme ve Masraf
                CREATE TABLE IF NOT EXISTS ""CustomerRefunds"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""CustomerId"" INTEGER NOT NULL REFERENCES ""Customers""(""Id"") ON DELETE CASCADE,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Currency"" TEXT NOT NULL DEFAULT 'EUR',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""Reference"" TEXT NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""CreatedBy"" TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_CustomerRefunds_CustomerId"" ON ""CustomerRefunds"" (""CustomerId"");

                ALTER TABLE ""Expenses"" ADD COLUMN IF NOT EXISTS ""CustomerId"" INTEGER NULL REFERENCES ""Customers""(""Id"") ON DELETE SET NULL;
                CREATE INDEX IF NOT EXISTS ""IX_Expenses_CustomerId"" ON ""Expenses"" (""CustomerId"");

                -- v201: Manuel tahsilat tablosu
                CREATE TABLE IF NOT EXISTS ""CustomerCollections"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""CustomerId"" INTEGER NOT NULL REFERENCES ""Customers""(""Id"") ON DELETE CASCADE,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Currency"" TEXT NOT NULL DEFAULT 'EUR',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""Reference"" TEXT NULL,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""CreatedBy"" TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_CustomerCollections_CustomerId"" ON ""CustomerCollections"" (""CustomerId"");

                -- v202: Personel ödemeleri (Avans, Maaş)
                CREATE TABLE IF NOT EXISTS ""StaffPayments"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" TEXT NOT NULL REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE,
                    ""Type"" INTEGER NOT NULL DEFAULT 1,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Currency"" TEXT NOT NULL DEFAULT 'EUR',
                    ""PeriodYear"" INTEGER NULL,
                    ""PeriodMonth"" INTEGER NULL,
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""PaymentDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""CreatedBy"" TEXT NULL
                );
        CREATE INDEX IF NOT EXISTS ""IX_StaffPayments_UserId"" ON ""StaffPayments"" (""UserId"");
        CREATE INDEX IF NOT EXISTS ""IX_StaffPayments_CreatedDate"" ON ""StaffPayments"" (""CreatedDate"");

                -- v203: Personel profili (maaş / ödeme günü) + gider bağlantısı
                CREATE TABLE IF NOT EXISTS ""StaffProfiles"" (
                    ""UserId"" TEXT NOT NULL PRIMARY KEY REFERENCES ""AspNetUsers""(""Id"") ON DELETE CASCADE,
                    ""MonthlySalary"" NUMERIC(18,2) NULL,
                    ""SalaryDayOfMonth"" INTEGER NULL
                );
                ALTER TABLE ""StaffPayments"" ADD COLUMN IF NOT EXISTS ""ExpenseId"" INTEGER NULL REFERENCES ""Expenses""(""Id"") ON DELETE SET NULL;
                CREATE INDEX IF NOT EXISTS ""IX_StaffPayments_ExpenseId"" ON ""StaffPayments"" (""ExpenseId"");

                -- v204: Müşteri doğum günü (toplu e-posta segmenti)
                ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""DateOfBirth"" DATE NULL;

                -- v210: Harici site API entegrasyonu
                CREATE TABLE IF NOT EXISTS ""ApiClients"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Name"" TEXT NOT NULL DEFAULT '',
                    ""ApiKeyHash"" TEXT NOT NULL DEFAULT '',
                    ""ApiKeyPrefix"" TEXT NOT NULL DEFAULT '',
                    ""WebhookSecret"" TEXT NOT NULL DEFAULT '',
                    ""DefaultWebhookUrl"" TEXT NULL,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""CreatedByUserId"" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ApiClients_ApiKeyHash"" ON ""ApiClients"" (""ApiKeyHash"");

                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ApiClientId"" INTEGER NULL REFERENCES ""ApiClients""(""Id"") ON DELETE SET NULL;
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ExternalReference"" TEXT NULL;
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""MerchantWebhookUrl"" TEXT NULL;
                CREATE INDEX IF NOT EXISTS ""IX_Invoices_ApiClientId_OrderNumber"" ON ""Invoices"" (""ApiClientId"", ""OrderNumber"");

            ");
            logger.LogInformation("RecurringInvoices, ApprovalRequests, Expenses, SystemLogs, WithdrawalRequests tabloları kontrol edildi/oluşturuldu.");
        }
        else
        {
            // SQLite (Local): Normal migration
            db.Database.Migrate();
        }
        logger.LogInformation("Database migration completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed");
    }
}


// Rol ve seed kullanıcı oluşturma
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    // Rolleri oluştur ve Name alanını doğru case ile düzelt
    foreach (var roleName in new[] { "MasterAdmin", "Admin", "Personel" })
    {
        var existingRole = await roleManager.FindByNameAsync(roleName);
        if (existingRole == null)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
        else if (existingRole.Name != roleName)
        {
            // DB'deki Name alanı yanlış case ile kayıtlı (örn: "masterAdmin" → "MasterAdmin")
            existingRole.Name = roleName;
            await roleManager.UpdateAsync(existingRole);
            logger.LogWarning("Rol adı düzeltildi: {OldName} → {NewName}", existingRole.Name, roleName);
        }
    }

    // MasterAdmin seed kullanıcısı
    const string masterEmail = "govercinweb@gmail.com";
    const string masterPassword = "Master@2024!";
    var masterUser = await userManager.FindByEmailAsync(masterEmail);
    if (masterUser == null)
    {
        masterUser = new User
        {
            FullName = "Master Admin",
            UserName = "masteradmin",
            Email = masterEmail,
            EmailConfirmed = true,
            IsActive = true
        };
        var result = await userManager.CreateAsync(masterUser, masterPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(masterUser, "MasterAdmin");
            await userManager.UpdateSecurityStampAsync(masterUser);
            logger.LogInformation("MasterAdmin kullanıcısı oluşturuldu: {Email}", masterEmail);
        }
        else
        {
            logger.LogError("MasterAdmin oluşturulamadı: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        // Kullanıcı var — IsActive ve EmailConfirmed garantile
        bool changed = false;
        if (!masterUser.IsActive) { masterUser.IsActive = true; changed = true; }
        if (!masterUser.EmailConfirmed) { masterUser.EmailConfirmed = true; changed = true; }
        if (changed) await userManager.UpdateAsync(masterUser);

        // MasterAdmin rolü yoksa ekle
        if (!await userManager.IsInRoleAsync(masterUser, "MasterAdmin"))
        {
            var roleResult = await userManager.AddToRoleAsync(masterUser, "MasterAdmin");
            logger.LogInformation("MasterAdmin rolü eklendi: {Success}, Errors: {Errors}",
                roleResult.Succeeded,
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        // Her startup'ta SecurityStamp güncelle — eski cookie'leri geçersiz kıl
        await userManager.UpdateSecurityStampAsync(masterUser);
        logger.LogInformation("MasterAdmin hazır: {Email}, Rol: {HasRole}",
            masterEmail,
            await userManager.IsInRoleAsync(masterUser, "MasterAdmin"));
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Heroku'da HTTPS redirect proxy arkasında sorun çıkarır
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseMiddleware<Crypto_Payment.Middleware.RoleRefreshMiddleware>();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<Crypto_Payment.Hubs.ChatHub>("/chatHub");
app.MapHub<Crypto_Payment.Hubs.SupportChatHub>("/supportChatHub");

app.Run();
