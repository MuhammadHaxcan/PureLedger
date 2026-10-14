using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PureLedger.Data;
using PureLedger.Repositories;
using PureLedger.Services.Auth;
using PureLedger.ViewModels;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class UserEmailRacePostgresTests
{
    [PostgresFact]
    public async Task ConcurrentCreateUser_WithSameEmail_PersistsOnlyOneUser()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES"));
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"race-{suffix}@example.com";
        var options = new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(cs.ConnectionString).Options;

        // Several attempts: a single run can serialize by luck.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var attemptEmail = $"a{attempt}-{email}";
            await using var first = new LedgerDbContext(options);
            await using var second = new LedgerDbContext(options);
            var a = NewService(first);
            var b = NewService(second);

            var outcomes = await Task.WhenAll(
                a.CreateUserAsync(Model($"race-a-{attempt}-{suffix}", attemptEmail)),
                b.CreateUserAsync(Model($"race-b-{attempt}-{suffix}", attemptEmail)));

            await using var check = new LedgerDbContext(options);
            Assert.Equal(1, outcomes.Count(x => x));
            Assert.Equal(1, await check.Users.CountAsync(x => x.Email == attemptEmail));
        }
    }

    private static AuthService NewService(LedgerDbContext context) => new(
        new HttpContextAccessor(), new UnitOfWork(context), NullLogger<AuthService>.Instance,
        new ConfigurationBuilder().Build());

    private static UserViewModel Model(string username, string email) => new()
    {
        Username = username, Email = email, Password = "Synthetic test password", FullName = "Race test"
    };
}
