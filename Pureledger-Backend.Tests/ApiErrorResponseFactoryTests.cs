using Microsoft.AspNetCore.Http;
using PureLedger.Exceptions;
using PureLedger.Services;
using PureLedger.ViewModels.Reporting;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ApiErrorResponseFactoryTests
{
    private static (int Status, string Message, string Joined, string? ErrorId) Map(Exception exception)
    {
        var response = new ApiErrorResponseFactory().CreateFromException(
            new DefaultHttpContext(), exception, includeDeveloperDetails: false, out _);
        return (response.StatusCode, response.Message, string.Join(" | ", response.Errors), response.ErrorId);
    }

    private static Exception Thrown(Action action)
    {
        try { action(); }
        catch (Exception ex) { return ex; }
        throw new InvalidOperationException("The action did not throw.");
    }

    [Fact]
    public void FrameworkInvalidOperation_IsAnInternalErrorWithoutItsMessage()
    {
        var frameworkError = Thrown(() => Enumerable.Empty<int>().Single());

        var result = Map(frameworkError);

        Assert.Equal(500, result.Status);
        Assert.NotNull(result.ErrorId);
        Assert.DoesNotContain("Sequence", result.Message + result.Joined);
    }

    [Fact]
    public void FrameworkArgumentAndKeyErrors_AreInternalErrorsWithoutTheirMessages()
    {
        var argument = Thrown(() => _ = new List<int>(-5));
        var missingKey = Thrown(() => _ = new Dictionary<string, int>()["secret-key"]);

        foreach (var error in new[] { argument, missingKey })
        {
            var result = Map(error);
            Assert.Equal(500, result.Status);
            Assert.DoesNotContain("secret-key", result.Message + result.Joined);
            Assert.DoesNotContain("capacity", result.Message + result.Joined);
        }
    }

    [Fact]
    public void ApplicationRaisedInvalidOperation_KeepsItsUserMessageAsABadRequest()
    {
        // AgingFifoCalculator raises this deliberately for a negative amount.
        var businessError = Thrown(() => AgingFifoCalculator.Calculate(new[]
        {
            new AgingFifoMovement { JournalEntryId = 9, Debit = -1 }
        }));

        var result = Map(businessError);

        Assert.True(ExceptionClassifier.IsApplicationRaised(businessError));
        Assert.Equal(400, result.Status);
        Assert.Contains("negative debit or credit", result.Joined);
        Assert.Null(result.ErrorId);
    }

    [Fact]
    public void AccessDenialIs403_AndOnlyMissingSignInIs401()
    {
        Assert.Equal(403, Map(new UnauthorizedAccessException("You do not have access to this project.")).Status);
        Assert.Equal(401, Map(new NotAuthenticatedException()).Status);
        // A framework-raised denial never shows its text (for example a file-system path).
        var framework = Thrown(() => throw new UnauthorizedAccessException(@"Access to the path 'C:\secret' is denied."));
        Assert.DoesNotContain("secret", Map(framework).Joined);
    }

    [Fact]
    public void UserMessage_DropsTheParameterSuffix()
    {
        var error = new ArgumentException("Station must be 250 characters or fewer.", "station");

        Assert.Equal("Station must be 250 characters or fewer.", ExceptionClassifier.UserMessage(error));
    }
}
