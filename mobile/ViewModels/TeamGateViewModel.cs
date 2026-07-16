using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>
/// Shown right after login. Loads the account's teams: zero shows a create-team form,
/// exactly one is picked automatically, more than one shows a picker (mirrors the Web
/// app's create-team / select-team screens).
/// </summary>
public partial class TeamGateViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    private string newTeamName = string.Empty;

    [ObservableProperty]
    private bool showPicker;

    [ObservableProperty]
    private bool showCreateForm;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<TeamSummary> Teams { get; } = new();

    public TeamGateViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var teams = await _api.GetMyTeamsAsync();
            Teams.Clear();
            foreach (var team in teams)
            {
                Teams.Add(team);
            }

            if (teams.Count == 1)
            {
                await SelectTeamAsync(teams[0]);
                return;
            }

            if (teams.Count == 0)
            {
                ShowCreateForm = true;
                ShowPicker = false;
            }
            else
            {
                ShowPicker = true;
                ShowCreateForm = false;
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load your teams. {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectTeamAsync(TeamSummary team)
    {
        AppSettingsStore.SetCurrentTeam(team.Id, team.Name, team.Role);

        // If a team admin has already linked this login to a roster employee, use that —
        // no need to make the person type in their own employee code. Best-effort: if this
        // fails for any reason, the manual field in Profile still works as a fallback.
        try
        {
            var me = await _api.GetMeAsync();
            if (!string.IsNullOrWhiteSpace(me?.Code))
            {
                AppSettingsStore.MemberCode = me.Code;
            }
        }
        catch
        {
            // Fall through — Profile's manual entry still covers this.
        }

        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//main/roster");
        }
    }

    [RelayCommand]
    private async Task CreateTeamAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(NewTeamName))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var team = await _api.CreateTeamAsync(NewTeamName.Trim());
            await SelectTeamAsync(team);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't create the team. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowCreateInstead()
    {
        ShowPicker = false;
        ShowCreateForm = true;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        SecureTokenStore.ClearToken();
        AppSettingsStore.ClearSession();
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//login");
        }
    }
}
