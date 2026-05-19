using Crypto_Payment.Data;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Tests.Helpers;

/// <summary>
/// Her test için benzersiz bir InMemory veritabanı ile AppDbContext örneği oluşturur.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Benzersiz bir InMemory veritabanı adı ile yeni bir AppDbContext oluşturur.
    /// Her çağrıda izole bir veritabanı sağlar.
    /// </summary>
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
