using Crypto_Payment.Manager;
using Microsoft.EntityFrameworkCore;
using Crypto_Payment.Data;
using Crypto_Payment.Helpers;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Npgsql;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

Console.WriteLine("DATABASE_URL var mi? " +
                  (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL"))));


var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var csb = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Require,
            TrustServerCertificate = true
        };

        opt.UseNpgsql(csb.ConnectionString);
    }
    else
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=invoice.db"));
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

builder.Services.AddHttpClient<IPlisioService, PlisioManager>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/api/auth/login";
    options.AccessDeniedPath = "/api/auth/denied";
});

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ICustomerService, CustomerManager>();
builder.Services.AddScoped<IInvoiceService, InvoiceManager>();
builder.Services.AddScoped<IRoleService, RoleManager>();

var app = builder.Build();

Console.WriteLine("scope start");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    Console.WriteLine("DB PROVIDER: " + db.Database.ProviderName);

    try
    {
        Console.WriteLine("DB MIGRATE START");
        db.Database.Migrate();
        Console.WriteLine("DB MIGRATE OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB MIGRATE FAIL >>> " + ex.Message);
        Console.WriteLine(ex.ToString());
    }
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
