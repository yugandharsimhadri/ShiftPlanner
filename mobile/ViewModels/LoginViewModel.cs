using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private string apiBaseUrl = AppSettingsStore.ApiBaseUrl;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    [NotifyPropertyChangedFor(nameof(SubmitText))]
    [NotifyPropertyChangedFor(nameof(ToggleModeText))]
    private bool isRegisterMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(SubmitText))]
    private bool isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotBusy => !IsBusy;

    public string SubtitleText => IsRegisterMode ? "Create an account to join or start a team" : "Sign in to see your shifts";

    public string SubmitText => IsBusy ? "One moment…" : IsRegisterMode ? "Create account" : "Sign in";

    public string ToggleModeText => IsRegisterMode ? "Already have an account? Sign in" : "New here? Create an account";

    public LoginViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegisterMode = !IsRegisterMode;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            ErrorMessage = "Enter your ShiftPlanner server address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your email and password.";
            return;
        }

        try
        {
            IsBusy = true;

            // Persist the server address before calling the API - ApiClient reads it fresh
            // from AppSettingsStore on every request.
            AppSettingsStore.ApiBaseUrl = ApiBaseUrl.Trim();

            if (IsRegisterMode)
            {
                await _api.RegisterAsync(Email.Trim(), Password);
            }

            var response = await _api.LoginAsync(Email.Trim(), Password);

            var lifetime = response.ExpiresIn > 0 ? response.ExpiresIn : 3600;
            var expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(lifetime);
            await SecureTokenStore.SaveTokenAsync(response.AccessToken, expiresAtUtc);

            AppSettingsStore.UserEmail = Email.Trim();
            Password = string.Empty;

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//teamgate");
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Something went wrong signing in: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
