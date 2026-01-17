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
        options.SignIn.RequireConfirmedEmail = true; // istersen false yap
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders(); // Email confirm + 2FA token providerlar

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

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        Console.WriteLine("MIGRATE OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine("MIGRATE FAIL: " + ex.Message);
        Console.WriteLine(ex.ToString());
        throw; // burada throw kalsın ki deploy fail olup hatayı net görelim
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Static dosyalar burada çözülür

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// app.MapControllerRoute(
//     name: "default",
//     pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();