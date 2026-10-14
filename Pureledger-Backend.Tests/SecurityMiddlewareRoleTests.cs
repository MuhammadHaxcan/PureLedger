using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Middleware;
using PureLedger.Models;
using PureLedger.Services.Auth;
using PureLedger.Services.Interface;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class SecurityMiddlewareRoleTests
{
    [Theory]
    [InlineData("Admin", false, false)] // demoted after the token was issued
    [InlineData("User", true, true)]    // promoted after the token was issued
    [InlineData("Admin", true, true)]
    [InlineData("User", false, false)]
    public async Task AdminRights_FollowTheDatabaseNotTheTokenRole(string tokenRole, bool adminInDatabase, bool expected)
    {
        var auth = DispatchProxy.Create<IAuthService, AuthProxy>();
        ((AuthProxy)(object)auth).Stored = new User { UserId = 7, Username = "u", IsActive = true, IsAdmin = adminInDatabase };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, tokenRole)
            }, "test"))
        };
        var seenByEndpoint = (bool?)null;
        var middleware = new SecurityMiddleware(ctx =>
        {
            // The same check the approval, settings and report controllers use.
            seenByEndpoint = new CurrentUserService(new HttpContextAccessor { HttpContext = ctx }).IsCurrentUserAdmin();
            return Task.CompletedTask;
        }, NullLogger<SecurityMiddleware>.Instance);

        await middleware.InvokeAsync(context, auth, null!);

        Assert.Equal(expected, seenByEndpoint);
        Assert.Equal(expected, context.User.IsInRole("Admin"));
        Assert.Single(context.User.FindAll(ClaimTypes.Role));
    }

    public class AuthProxy : DispatchProxy
    {
        public User? Stored { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(IAuthService.GetUserByIdAsync)
                ? Task.FromResult(Stored)
                : throw new NotSupportedException(method?.Name);
    }
}
