using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;
using Xunit;

namespace ShiftPlanner.Api.Tests;

// Pure unit tests for the expiry math — no HTTP round trip, so timing is fully
// controlled rather than depending on wall-clock waits.
public class AvailabilityServiceTests
{
    [Theory]
    [InlineData("Asia/Kolkata", 9)]
    [InlineData("asia/kolkata", 9)] // case-insensitive
    [InlineData("Asia/Calcutta", 9)] // pre-1995 IANA alias some ICU builds still resolve to
    [InlineData("America/New_York", 8)]
    [InlineData(null, 8)]
    public void Default_auto_expiry_hours_is_9_for_India_and_8_otherwise(string? timezone, int expected)
    {
        Assert.Equal(expected, AvailabilityService.DefaultAutoExpiryHours(timezone));
    }

    [Fact]
    public void Effective_hours_uses_override_when_set()
    {
        var person = new Person { Timezone = "Asia/Kolkata", AvailabilityAutoExpiryHoursOverride = 3 };
        Assert.Equal(3, AvailabilityService.EffectiveAutoExpiryHours(person));
    }

    [Fact]
    public void Effective_hours_falls_back_to_timezone_default_when_no_override()
    {
        var person = new Person { Timezone = "America/New_York", AvailabilityAutoExpiryHoursOverride = null };
        Assert.Equal(8, AvailabilityService.EffectiveAutoExpiryHours(person));
    }

    [Fact]
    public void Not_available_when_never_toggled_on()
    {
        var member = new TeamMember { IsAvailable = false, AvailableSince = null };
        var person = new Person();
        Assert.False(AvailabilityService.IsEffectivelyAvailable(member, person, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Available_within_the_expiry_window()
    {
        var now = DateTimeOffset.UtcNow;
        var member = new TeamMember { IsAvailable = true, AvailableSince = now.AddHours(-2) };
        var person = new Person { Timezone = "Asia/Kolkata" }; // 9h default
        Assert.True(AvailabilityService.IsEffectivelyAvailable(member, person, now));
    }

    [Fact]
    public void No_longer_available_once_past_the_expiry_window()
    {
        var now = DateTimeOffset.UtcNow;
        var member = new TeamMember { IsAvailable = true, AvailableSince = now.AddHours(-10) };
        var person = new Person { Timezone = "Asia/Kolkata" }; // 9h default
        Assert.False(AvailabilityService.IsEffectivelyAvailable(member, person, now));
    }
}
