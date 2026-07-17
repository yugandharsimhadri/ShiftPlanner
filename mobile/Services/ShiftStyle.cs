namespace ShiftPlanner.Mobile.Services;

/// <summary>
/// Maps a shift code to the app's fixed chip colors (morning/evening/night/off/leave).
/// These colors are semantic status colors and, per design, stay the same in light and
/// dark mode so a shift is recognizable at a glance regardless of theme.
/// If the shift code isn't one we recognize, falls back to the color the server sent.
/// </summary>
public static class ShiftStyle
{
    private static readonly Color MorningFg = Color.FromArgb("#A8701F");
    private static readonly Color MorningBg = Color.FromArgb("#FBF0DD");

    private static readonly Color EveningFg = Color.FromArgb("#4453AD");
    private static readonly Color EveningBg = Color.FromArgb("#E7E9F7");

    private static readonly Color NightFg = Color.FromArgb("#22314F");
    private static readonly Color NightBg = Color.FromArgb("#E3E6ED");

    private static readonly Color OffFg = Color.FromArgb("#6E7D78");
    private static readonly Color OffBg = Color.FromArgb("#EEF1F0");

    private static readonly Color LeaveFg = Color.FromArgb("#A5392B");
    private static readonly Color LeaveBg = Color.FromArgb("#FBE7E3");

    private static readonly Color HolidayFg = Color.FromArgb("#6B4C93");
    private static readonly Color HolidayBg = Color.FromArgb("#EFE7F5");

    private static readonly Color UnassignedFg = Color.FromArgb("#8A6D1B");
    private static readonly Color UnassignedBg = Color.FromArgb("#FBF3DC");

    /// <summary>Why a member has no shift on a given day — distinguishes states that
    /// otherwise all look identical ("nothing assigned"), matching Web's shift-context
    /// clarity: on approved leave, a team holiday, their default off day, or genuinely
    /// unassigned on a working day (the one that most needs a manager's attention).</summary>
    public enum NoShiftContext
    {
        Unassigned,
        DefaultOff,
        Holiday,
        Leave,
    }

    public static string LabelFor(NoShiftContext context) => context switch
    {
        NoShiftContext.Leave => "On leave",
        NoShiftContext.Holiday => "Holiday",
        NoShiftContext.DefaultOff => "Off",
        _ => "Unassigned",
    };

    public static (Color Foreground, Color Background) ResolveContext(NoShiftContext context) => context switch
    {
        NoShiftContext.Leave => (LeaveFg, LeaveBg),
        NoShiftContext.Holiday => (HolidayFg, HolidayBg),
        NoShiftContext.DefaultOff => (OffFg, OffBg),
        _ => (UnassignedFg, UnassignedBg),
    };

    public static (Color Foreground, Color Background) Resolve(string? shiftCode, string? serverColorHex)
    {
        var normalized = (shiftCode ?? string.Empty).Trim().ToUpperInvariant();

        return normalized switch
        {
            "M" or "MORNING" or "AM" => (MorningFg, MorningBg),
            "E" or "EVENING" or "PM" => (EveningFg, EveningBg),
            "N" or "NIGHT" => (NightFg, NightBg),
            "O" or "OFF" or "DAYOFF" or "REST" => (OffFg, OffBg),
            "L" or "LEAVE" or "PTO" or "VACATION" => (LeaveFg, LeaveBg),
            _ => FromServerColor(serverColorHex),
        };
    }

    private static (Color, Color) FromServerColor(string? serverColorHex)
    {
        if (!string.IsNullOrWhiteSpace(serverColorHex) && Color.TryParse(serverColorHex, out var background))
        {
            var isLightBackground = background.GetLuminosity() > 0.6;
            var foreground = isLightBackground ? Color.FromArgb("#10201D") : Color.FromArgb("#FFFFFF");
            return (foreground, background);
        }

        return (OffFg, OffBg);
    }
}
