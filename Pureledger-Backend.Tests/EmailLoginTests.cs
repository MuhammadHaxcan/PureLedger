using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Controllers.Api;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services.Auth;
using PureLedger.ViewModels;
using Xunit;
using LoginViewModel = PureLedger.ViewModels.Auth.LoginViewModel;

namespace PureLedger.Backend.Tests;

public sealed class EmailLoginTests
{
    private const string Password = "Test password with spaces ";
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

    [Theory]
    [InlineData("bookkeeper")]
    [InlineData(" BOOKKEEPER ")]
    [InlineData("bookkeeper@example.com")]
    [InlineData(" BOOKKEEPER@EXAMPLE.COM ")]
    public async Task Login_AcceptsUsernameOrEmailAndReturnsCanonicalIdentity(string identifier)
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        user.UserProjects.Add(new UserProject { ProjectId = 7 });
        user.ControllerAccess.Add(new UserControllerAccess { Controller = "Reporting", Action = "AccountReport" });
        user.UserCategories.Add(new UserCategory { CategoryId = 8 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var httpContext = new DefaultHttpContext();
        var service = CreateService(context, httpContext);
        var controller = new AccountApiController(service, NullLogger<AccountApiController>.Instance, Configuration())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.Login(new LoginViewModel
        {
            Username = identifier, Password = Password
        }));

        var response = JsonSerializer.SerializeToElement(result.Value);
        Assert.True(response.GetProperty("success").GetBoolean());
        Assert.Equal("bookkeeper", response.GetProperty("user").GetProperty("username").GetString());
        Assert.Equal(user.UserId, response.GetProperty("user").GetProperty("userId").GetInt32());
        var persistedUser = await context.Users.FindAsync(user.UserId);
        Assert.NotNull(persistedUser!.LastLoginDate);
        Assert.Single(persistedUser.UserProjects);
        Assert.Single(persistedUser.ControllerAccess);
        Assert.Single(persistedUser.UserCategories);
        var refreshToken = await context.RefreshTokens.SingleAsync();
        Assert.Equal(user.UserId, refreshToken.UserId);
        var cookies = httpContext.Response.Headers.SetCookie.ToArray();
        Assert.Contains(cookies, c => c!.StartsWith("refreshToken=") && c.Contains("httponly"));
        var accessCookie = Assert.Single(cookies.Where(c => c!.StartsWith("accessToken=")));
        var token = accessCookie!.Split(';')[0]["accessToken=".Length..];
        var principal = service.ValidateToken(token);
        Assert.Equal("bookkeeper", principal!.Identity!.Name);
        Assert.Equal(user.UserId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    [Theory]
    [InlineData("missing@example.com", Password, true, "Default")]
    [InlineData("bookkeeper@example.com", "wrong-password", true, "Default")]
    [InlineData("bookkeeper@example.com", "Test password with spaces", true, "Default")]
    [InlineData("bookkeeper@example.com", Password, false, "Default")]
    [InlineData("bookkeeper@example.com", Password, true, "Other")]
    [InlineData("bookkeeper", "wrong-password", true, "Default")]
    [InlineData(" ", Password, true, "Default")]
    [InlineData("bookkeeper@example.com", "", true, "Default")]
    public async Task Login_RejectsInvalidCredentialsInactiveUsersAndWrongSubroutes(
        string identifier, string password, bool active, string subroute)
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        user.IsActive = active;
        user.Subroute = subroute;
        await context.SaveChangesAsync();
        var httpContext = new DefaultHttpContext();
        var service = CreateService(context, httpContext);
        var controller = new AccountApiController(service, NullLogger<AccountApiController>.Instance, Configuration())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginViewModel
        {
            Username = identifier, Password = password
        }));

        Assert.Equal("Invalid username/email or password",
            JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString());
        Assert.Null(user.LastLoginDate);
        Assert.Empty(context.RefreshTokens);
        Assert.False(httpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Login_PreservesAdminSubrouteAccessAndSupportsLegacyPaddedEmails()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        user.IsAdmin = true;
        user.Subroute = "Other";
        user.Email = " Bookkeeper@Example.com ";
        await context.SaveChangesAsync();

        Assert.NotNull(await CreateService(context).AuthenticateUserAsync(new LoginViewModel
        {
            Username = "bookkeeper@example.com", Password = Password
        }));
    }

    [Fact]
    public async Task Login_RejectsDuplicateEmailsButKeepsDistinctUsernamesUsable()
    {
        await using var context = CreateContext();
        await SeedUserAsync(context);
        await SeedUserAsync(context, "second-user", " BOOKKEEPER@EXAMPLE.COM ");
        var service = CreateService(context);

        Assert.Null(await service.AuthenticateUserAsync(new LoginViewModel
        {
            Username = "bookkeeper@example.com", Password = Password
        }));
        Assert.NotNull(await service.AuthenticateUserAsync(new LoginViewModel
        {
            Username = "second-user", Password = Password
        }));
    }

    [Fact]
    public async Task Login_RejectsUsernameEmailCollisionsAcrossAccounts()
    {
        await using var context = CreateContext();
        await SeedUserAsync(context);
        await SeedUserAsync(context, "bookkeeper@example.com", "second@example.com");

        Assert.Null(await CreateService(context).AuthenticateUserAsync(new LoginViewModel
        {
            Username = "bookkeeper@example.com", Password = Password
        }));
    }

    [Fact]
    public async Task Login_AllowsSameAccountUsernameAndEmailToMatchAndEmailsToBeOptional()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context, "person@example.com", "person@example.com");
        await SeedUserAsync(context, "no-email", null);
        var service = CreateService(context);

        Assert.Equal(user.UserId, (await service.AuthenticateUserAsync(new LoginViewModel
        {
            Username = "person@example.com", Password = Password
        }))!.UserId);
        Assert.NotNull(await service.AuthenticateUserAsync(new LoginViewModel
        {
            Username = "no-email", Password = Password
        }));
    }

    [Theory]
    [InlineData("new-user", " BOOKKEEPER@EXAMPLE.COM ")]
    [InlineData("BOOKKEEPER@EXAMPLE.COM", "new@example.com")]
    [InlineData("new-user", "BOOKKEEPER")]
    public async Task CreateUser_PreventsDuplicateOrConflictingLoginIdentifiers(string username, string email)
    {
        await using var context = CreateContext();
        await SeedUserAsync(context);

        Assert.False(await CreateService(context).CreateUserAsync(new UserViewModel
        {
            Username = username, Email = email, FullName = "New User", Password = Password
        }));
        Assert.Single(context.Users);
    }

    [Fact]
    public async Task UpdateUser_PreventsConflictsAndTrimsEmailWithoutChangingUsername()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        await SeedUserAsync(context, "second-user", "second@example.com");
        var service = CreateService(context);
        var model = new UserViewModel { UserId = user.UserId, FullName = user.FullName, Email = " SECOND@EXAMPLE.COM " };

        Assert.False(await service.UpdateUserAsync(model));
        Assert.Equal("bookkeeper@example.com", user.Email);
        model.Email = " Bookkeeper@Example.com ";
        Assert.True(await service.UpdateUserAsync(model));
        Assert.Equal("Bookkeeper@Example.com", user.Email);
        Assert.Equal("bookkeeper", user.Username);
        model.Email = "new@example.com";
        Assert.True(await service.UpdateUserAsync(model));
        Assert.Null(await service.AuthenticateUserAsync(new LoginViewModel
        {
            Username = "bookkeeper@example.com", Password = Password
        }));
        Assert.NotNull(await service.AuthenticateUserAsync(new LoginViewModel
        {
            Username = "new@example.com", Password = Password
        }));
    }

    [Fact]
    public async Task UpdateUser_WithUnchangedLegacyDuplicateEmail_StillSavesOtherChanges()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context, "first", "shared@example.com");
        await SeedUserAsync(context, "second", "shared@example.com");
        var service = CreateService(context);
        var model = new UserViewModel { UserId = user.UserId, FullName = "Renamed", Email = " SHARED@example.com " };

        Assert.True(await service.UpdateUserAsync(model));
        Assert.Equal("Renamed", user.FullName);
        model.Email = "second-new@example.com";
        Assert.True(await service.UpdateUserAsync(model));
        // Moving to an address another user owns is still refused.
        await SeedUserAsync(context, "third", "taken@example.com");
        model.Email = "taken@example.com";
        Assert.False(await service.UpdateUserAsync(model));
    }

    private static LedgerDbContext CreateContext() => new(new DbContextOptionsBuilder<LedgerDbContext>()
        .UseInMemoryDatabase($"email-login-{Guid.NewGuid():N}")
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static async Task<User> SeedUserAsync(LedgerDbContext context,
        string username = "bookkeeper", string? email = "bookkeeper@example.com")
    {
        var user = new User { Username = username, Email = email, FullName = "Book Keeper", PasswordHash = PasswordHash };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Jwt:SecretKey"] = "email-login-tests-secret-key-with-at-least-32-characters" }).Build();

    private static AuthService CreateService(LedgerDbContext context, HttpContext? httpContext = null) => new(
        new HttpContextAccessor { HttpContext = httpContext ?? new DefaultHttpContext() },
        new UnitOfWork(context), NullLogger<AuthService>.Instance, Configuration());
}
