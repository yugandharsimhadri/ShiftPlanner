using ShiftPlanner.Mobile.Services;
using ShiftPlanner.Mobile.Views;

namespace ShiftPlanner.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pushed on top of the Team Members tab (add/edit form) rather than being a tab
        // itself — registered here since it's not in the TabBar markup above.
        Routing.RegisterRoute("teammemberform", typeof(TeamMemberFormPage));

        Loaded += OnShellLoaded;
    }

    private async void OnShellLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnShellLoaded;

        // If we already have a valid, unexpired token, skip straight past the login page —
        // and past the team picker too if a team was already chosen last session.
        var hasSession = await SecureTokenStore.HasValidTokenAsync();
        if (hasSession)
        {
            await GoToAsync(AppSettingsStore.CurrentTeamId.HasValue ? "//main/roster" : "//teamgate");
        }
    }
}
