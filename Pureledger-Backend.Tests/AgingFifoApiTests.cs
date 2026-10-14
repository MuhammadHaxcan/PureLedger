using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Controllers.Api;
using PureLedger.Services.Auth;
using PureLedger.Services.Interface;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class AgingFifoApiTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    public async Task DetailRequiresProjectAndAgingPermission(bool project, bool controllerPermission, bool agingPermission)
    {
        await using var db = AgingFifoReportTests.CreateContext();
        AgingFifoReportTests.Seed(db); await db.SaveChangesAsync();
        var proxy = DispatchProxy.Create<IAuthService, AuthProxy>();
        var auth = (AuthProxy)(object)proxy;
        auth.Project = project; auth.Controller = controllerPermission; auth.Aging = agingPermission;
        Assert.IsType<ForbidResult>(await Controller(db, new()).GetAgingFifo(AgingFifoReportTests.Request(), proxy, default));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task AdminOrExplicitReportingAccessCanRead(bool admin, bool controllerPermission, bool agingPermission)
    {
        await using var db = AgingFifoReportTests.CreateContext();
        AgingFifoReportTests.Seed(db); await db.SaveChangesAsync();
        var proxy = DispatchProxy.Create<IAuthService, AuthProxy>();
        var auth = (AuthProxy)(object)proxy;
        auth.Project = !admin; auth.Controller = controllerPermission; auth.Aging = agingPermission;
        Assert.IsType<OkObjectResult>(await Controller(db, new() { Admin = admin }).GetAgingFifo(AgingFifoReportTests.Request(), proxy, default));
        Assert.All(auth.Actions, action => Assert.Equal("AgingReport", action));
    }

    [Fact]
    public async Task MissingSessionIsUnauthorized_AndBadScopeOrMismatchedAccountIsRejected()
    {
        await using var db = AgingFifoReportTests.CreateContext();
        AgingFifoReportTests.Seed(db); await db.SaveChangesAsync();
        var proxy = DispatchProxy.Create<IAuthService, AuthProxy>();
        Assert.IsType<UnauthorizedResult>(await Controller(db, new() { UserId = null }).GetAgingFifo(AgingFifoReportTests.Request(), proxy, default));
        var controller = Controller(db, new() { Admin = true });
        var invalid = AgingFifoReportTests.Request("invalid");
        Assert.IsType<BadRequestObjectResult>(await controller.GetAgingFifo(invalid, proxy, default));
        invalid.Scope = "full"; invalid.AccountId = 999;
        Assert.IsType<NotFoundObjectResult>(await controller.GetAgingFifo(invalid, proxy, default));
    }

    private static ReportingApiController Controller(PureLedger.Data.LedgerDbContext db, CurrentUser user) => new(
        AgingFifoReportTests.Service(db), null!, null!, user, NullLogger<ReportingApiController>.Instance, null!, null!);
    public class AuthProxy : DispatchProxy
    {
        public bool Project { get; set; }
        public bool Controller { get; set; }
        public bool Aging { get; set; }
        public List<string> Actions { get; } = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IAuthService.HasProjectAccessAsync)) return Task.FromResult(Project);
            if (method?.Name == nameof(IAuthService.HasControllerAccessAsync)) return Task.FromResult(Controller);
            if (method?.Name == nameof(IAuthService.HasActionAccessAsync))
            {
                Actions.Add((string)args![2]!);
                return Task.FromResult(Aging && (string)args[1]! == "Reporting" && (string)args[2]! == "AgingReport");
            }
            throw new NotSupportedException(method?.Name);
        }
    }
    private sealed class CurrentUser : ICurrentUserService
    {
        public int? UserId { get; init; } = 42;
        public bool Admin { get; init; }
        public int? GetCurrentUserId() => UserId;
        public bool IsCurrentUserAdmin() => Admin;
        public string? GetCurrentUsername() => "tester";
    }
}
