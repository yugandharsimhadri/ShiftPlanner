using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Services;

// "Available" is self-toggled and time-boxed — nobody has to remember to turn it back
// off. A flag past its expiry window is treated as off everywhere it's read; nothing
// ever writes the stale value back, so there's no background sweep to run.
public static class AvailabilityService
{
    private const int DefaultHoursIndia = 9;
    private const int DefaultHoursElsewhere = 8;

    // "Asia/Calcutta" is the pre-1995 IANA name for the same zone — some ICU/browser
    // builds still resolve Intl.DateTimeFormat().resolvedOptions().timeZone to it.
    private static readonly string[] IndiaTimezoneNames = { "Asia/Kolkata", "Asia/Calcutta" };

    public static int DefaultAutoExpiryHours(string? timezone) =>
        timezone is not null && IndiaTimezoneNames.Contains(timezone, StringComparer.OrdinalIgnoreCase)
            ? DefaultHoursIndia
            : DefaultHoursElsewhere;

    public static int EffectiveAutoExpiryHours(Person person) =>
        person.AvailabilityAutoExpiryHoursOverride ?? DefaultAutoExpiryHours(person.Timezone);

    public static bool IsEffectivelyAvailable(TeamMember member, Person person, DateTimeOffset now)
    {
        if (!member.IsAvailable || member.AvailableSince is null) return false;
        var expiryHours = EffectiveAutoExpiryHours(person);
        return now - member.AvailableSince.Value < TimeSpan.FromHours(expiryHours);
    }
}
