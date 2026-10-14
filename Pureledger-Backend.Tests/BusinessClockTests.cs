using PureLedger.Helpers;
using Xunit;

namespace PureLedger.Backend.Tests;

[Collection("BusinessClock")]
public sealed class BusinessClockTests : IDisposable
{
    public void Dispose() => BusinessClock.Configure(null);

    [Fact]
    public void DefaultZone_IsPakistanAndToday_FollowsIt()
    {
        BusinessClock.Configure(null);

        Assert.Equal(TimeSpan.FromHours(5), BusinessClock.TimeZone.GetUtcOffset(DateTime.UtcNow));
        var expected = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        // A test running exactly at the boundary could straddle midnight; allow the neighbouring day only then.
        Assert.True(Math.Abs(BusinessClock.Today.DayNumber - expected.DayNumber) <= 1);
        Assert.Equal(expected, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, BusinessClock.TimeZone)));
    }

    [Fact]
    public void ConfiguredZone_ChangesTheCalendarDate()
    {
        BusinessClock.Configure("Pacific/Kiritimati"); // UTC+14, always at least one calendar day ahead of UTC-12
        var ahead = BusinessClock.Today;
        BusinessClock.Configure("Etc/GMT+12");          // UTC-12
        var behind = BusinessClock.Today;

        Assert.True(ahead.DayNumber - behind.DayNumber >= 1);
    }

    [Fact]
    public void UnknownZone_FailsLoudlyInsteadOfSilentlyUsingUtc()
    {
        Assert.Throws<InvalidOperationException>(() => BusinessClock.Configure("Not/AZone"));
    }
}
