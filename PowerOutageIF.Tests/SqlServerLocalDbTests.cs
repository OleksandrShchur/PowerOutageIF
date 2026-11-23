using Microsoft.EntityFrameworkCore;
using Xunit;
using PowerOutageIF.Data;
using PowerOutageIF.Models;

namespace PowerOutageIF.Tests;

public class SqlServerLocalDbTests
{
    [Fact]
    public async Task CanConnectAndSaveUserToLocalDb()
    {
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=PowerOutageIfTestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var testUser = new BotUser
        {
            Id = 999999999,
            FirstName = "TestUser",
            SubscribedQueue = "1.1",
            NotificationsEnabled = true,
            SilentModeAtNight = true
        };

        var existing = await db.Users.FindAsync(testUser.Id);
        if (existing != null)
        {
            db.Users.Remove(existing);
            await db.SaveChangesAsync();
        }

        db.Users.Add(testUser);
        await db.SaveChangesAsync();

        var loaded = await db.Users.FindAsync(testUser.Id);
        Assert.NotNull(loaded);
        Assert.Equal("1.1", loaded.SubscribedQueue);
        Assert.True(loaded.SilentModeAtNight);

        // Cleanup
        db.Users.Remove(loaded);
        await db.SaveChangesAsync();
    }
}
