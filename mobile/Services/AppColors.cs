namespace ShiftPlanner.Mobile.Services;

/// <summary>
/// C#-side mirror of the brand colors declared in Resources/Styles/Colors.xaml, for the
/// handful of view-model-computed colors (e.g. "today" ring on a day card) that can't be
/// expressed as a plain XAML AppThemeBinding. Keep these two files in sync.
/// </summary>
public static class AppColors
{
    private static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    public static Color PageBackground => IsDark ? Color.FromArgb("#121715") : Color.FromArgb("#F6F4EE");

    public static Color Surface => IsDark ? Color.FromArgb("#1B2320") : Color.FromArgb("#FFFFFF");

    public static Color Ink => IsDark ? Color.FromArgb("#ECF1EE") : Color.FromArgb("#232B28");

    public static Color InkSoft => IsDark ? Color.FromArgb("#AEBDB5") : Color.FromArgb("#5C6B63");

    public static Color Accent => IsDark ? Color.FromArgb("#4FAB90") : Color.FromArgb("#2F7D6B");

    public static Color Line => IsDark ? Color.FromArgb("#2A3733") : Color.FromArgb("#E7E2D6");
}
