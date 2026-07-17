using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private string userEmail = string.Empty;

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private string teamRole = string.Empty;

    [ObservableProperty]
    private string memberCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    [ObservableProperty]
    private string calendarFeedUrl = string.Empty;

    [ObservableProperty]
    private bool isLoadingCalendarFeed;

    public ProfileViewModel(ApiClient api)
    {
        _api = api;
        Refresh();
    }

    /// <summary>Re-reads persisted settings. Called from the page's OnAppearing so a fresh
    /// login (which happens before this tab is ever shown) is reflected.</summary>
    public void Refresh()
    {
        UserEmail = AppSettingsStore.UserEmail ?? "Not signed in";
        ApiBaseUrl = AppSettingsStore.ApiBaseUrl;
        TeamName = AppSettingsStore.CurrentTeamName ?? "No team selected";
        TeamRole = AppSettingsStore.CurrentTeamRole ?? "";
        MemberCode = AppSettingsStore.MemberCode ?? string.Empty;
        StatusMessage = null;
    }

    [RelayCommand]
    private void SaveServerAddress()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            StatusMessage = "Enter a server address.";
            return;
        }

        AppSettingsStore.ApiBaseUrl = ApiBaseUrl.Trim();
        StatusMessage = "Server address saved.";
    }

    [RelayCommand]
    private void SaveMemberCode()
    {
        AppSettingsStore.MemberCode = MemberCode;
        StatusMessage = string.IsNullOrWhiteSpace(MemberCode)
            ? "Cleared — Roster's \"Just me\" filter will be empty until this is set."
            : "Saved — Roster's \"Just me\" filter will now show your shifts.";
    }

    [RelayCommand]
    private async Task SwitchTeamAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//teamgate");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        SecureTokenStore.ClearToken();
        AppSettingsStore.ClearSession();
        Refresh();

        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//login");
        }
    }

    [RelayCommand]
    private async Task LoadCalendarFeedAsync()
    {
        try
        {
            IsLoadingCalendarFeed = true;
            CalendarFeedUrl = await _api.GetCalendarFeedUrlAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load your calendar feed link. {ex.Message}";
        }
        finally
        {
            IsLoadingCalendarFeed = false;
        }
    }

    [RelayCommand]
    private async Task CopyCalendarLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(CalendarFeedUrl))
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(CalendarFeedUrl);
        StatusMessage = "Calendar link copied.";
    }

    [RelayCommand]
    private static async Task ViewManagerDashboardAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("managerdashboard");
        }
    }
}
