using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    [ObservableProperty]
    private string userEmail = string.Empty;

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string teamName = string.Empty;

    [ObservableProperty]
    private string teamRole = string.Empty;

    [ObservableProperty]
    private string employeeCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ProfileViewModel()
    {
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
        EmployeeCode = AppSettingsStore.EmployeeCode ?? string.Empty;
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
    private void SaveEmployeeCode()
    {
        AppSettingsStore.EmployeeCode = EmployeeCode;
        StatusMessage = string.IsNullOrWhiteSpace(EmployeeCode)
            ? "Cleared — My Shifts will be empty until this is set."
            : "Saved — My Shifts will now show that employee's roster.";
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
}
