using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PureLedger.Controllers.Api;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Auth;
using PureLedger.Services.Interface;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ProjectImgTests
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGMQ8m4CAAFSAOBxZnXTAAAAAElFTkSuQmCC";
    private const string Jpeg = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAb/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAgP/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCwAAf/2Q==";

    [Fact]
    public async Task Logo_CanBeCreatedReplacedAndRemoved_WithoutASecondRow()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        Assert.IsType<OkObjectResult>(await controller.Put(1, new() { ImageDataUrl = Png }));
        Assert.Equal(Png, (await context.ProjectImgs.SingleAsync()).ImageDataUrl);

        Assert.IsType<OkObjectResult>(await controller.Put(1, new() { ImageDataUrl = Jpeg }));
        Assert.Equal(1, await context.ProjectImgs.CountAsync());
        Assert.Equal(Jpeg, (await context.ProjectImgs.SingleAsync()).ImageDataUrl);

        Assert.IsType<OkObjectResult>(await controller.Delete(1));
        Assert.Empty(await context.ProjectImgs.ToListAsync());
        Assert.IsType<OkObjectResult>(await controller.Get(1));
    }

    [Fact]
    public async Task Logos_RemainSeparateAcrossProjects()
    {
        await using var context = CreateContext();
        context.Projects.Add(new Project { ProjectId = 2, ProjectName = "Second project" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        await controller.Put(1, new() { ImageDataUrl = Png });
        await controller.Put(2, new() { ImageDataUrl = Jpeg });
        await controller.Delete(1);

        Assert.Null(await context.ProjectImgs.FindAsync(1));
        Assert.Equal(Jpeg, (await context.ProjectImgs.FindAsync(2))?.ImageDataUrl);
    }

    [Theory]
    [InlineData("data:image/png;base64,bm90LWEtcG5n")]
    [InlineData("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLytQAAAABJRU5ErkJggg==")]
    [InlineData("data:image/gif;base64,R0lGODlh")]
    [InlineData("data:image/jpeg;base64,not base64")]
    [InlineData("")]
    public async Task Logo_RejectsInvalidImage(string image)
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        Assert.IsType<BadRequestObjectResult>(await controller.Put(1, new() { ImageDataUrl = image }));
        Assert.Empty(await context.ProjectImgs.ToListAsync());
    }

    [Fact]
    public async Task Logo_RequiresProjectAccessAndProjectsPermissionForChanges()
    {
        await using var context = CreateContext();
        var proxy = DispatchProxy.Create<IAuthService, AuthProxy>();
        var auth = (AuthProxy)(object)proxy;
        var controller = new ProjectImgApiController(context, proxy, new TestCurrentUser());

        auth.ProjectAccess = false;
        Assert.IsType<ForbidResult>(await controller.Get(1));
        Assert.IsType<ForbidResult>(await controller.Put(1, new() { ImageDataUrl = Png }));

        auth.ProjectAccess = true;
        auth.ProjectsPermission = false;
        Assert.IsType<OkObjectResult>(await controller.Get(1));
        Assert.IsType<ForbidResult>(await controller.Put(1, new() { ImageDataUrl = Png }));
        Assert.IsType<ForbidResult>(await controller.Delete(1));
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new LedgerDbContext(options);
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Test project" });
        context.SaveChanges();
        return context;
    }

    private static ProjectImgApiController CreateController(LedgerDbContext context)
    {
        var proxy = DispatchProxy.Create<IAuthService, AuthProxy>();
        var auth = (AuthProxy)(object)proxy;
        auth.ProjectAccess = true;
        auth.ProjectsPermission = true;
        return new ProjectImgApiController(context, proxy, new TestCurrentUser());
    }

    public class AuthProxy : DispatchProxy
    {
        public bool ProjectAccess { get; set; }
        public bool ProjectsPermission { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IAuthService.HasProjectAccessAsync) => Task.FromResult(ProjectAccess),
            nameof(IAuthService.HasControllerAccessAsync) => Task.FromResult(false),
            nameof(IAuthService.HasActionAccessAsync) => Task.FromResult(ProjectsPermission),
            _ => throw new NotSupportedException(method?.Name)
        };
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public int? GetCurrentUserId() => 42;
        public bool IsCurrentUserAdmin() => false;
        public string? GetCurrentUsername() => "tester";
    }
}
