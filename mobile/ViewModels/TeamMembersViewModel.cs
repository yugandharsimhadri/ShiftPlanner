using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>
/// Team member directory — visible to everyone, but adding/editing and role changes are
/// Editor/Admin-only, mirroring the server's RequireTeamEditor()/RequireTeamAdmin() checks.
/// Replaces the old separate Employees list and Team (login/role) tab, which both showed
/// slices of the same underlying record.
/// </summary>
public partial class TeamMembersViewModel : ObservableObject
{
    private static readonly string[] Roles = { "Viewer", "Editor", "Admin" };

    private readonly ApiClient _api;
    private List<TeamMemberListItemViewModel> _all = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    private bool isAdmin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<TeamMemberListItemViewModel> Members { get; } = new();

    public TeamMembersViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsRefreshing = true;
            ErrorMessage = null;
            CanEdit = AppSettingsStore.CurrentTeamCanEdit;
            IsAdmin = AppSettingsStore.CurrentTeamIsAdmin;

            var members = await _api.GetTeamMembersAsync();
            _all = members
                .Select(m => new TeamMemberListItemViewModel
                {
                    Id = m.Id,
                    Code = m.Code,
                    Name = m.Name,
                    TrackName = m.TrackName ?? "",
                    JobRoleName = m.JobRoleName ?? "",
                    LocationName = m.LocationName ?? "",
                    Status = m.Status,
                    AccessRole = m.AccessRole,
                    HasLogin = m.HasLogin,
                })
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load team members. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Members.Clear();

        IEnumerable<TeamMemberListItemViewModel> source = _all;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            source = source.Where(m =>
                m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.TrackName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var member in source)
        {
            Members.Add(member);
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("teammemberform");
        }
    }

    [RelayCommand]
    private async Task OpenAsync(TeamMemberListItemViewModel item)
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (CanEdit)
        {
            await Shell.Current.GoToAsync($"teammemberform?id={item.Id}");
        }
        else
        {
            await Shell.Current.DisplayAlert(item.Name,
                $"Code: {item.Code}\nTrack: {item.TrackName}\nRole: {item.AccessRole}\nStatus: {item.Status}", "OK");
        }
    }

    [RelayCommand]
    private async Task ChangeRoleAsync(TeamMemberListItemViewModel item)
    {
        if (!IsAdmin || Shell.Current is null)
        {
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet($"Access role for {item.Name}", "Cancel", null, Roles);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel" || choice == item.AccessRole)
        {
            return;
        }

        try
        {
            await _api.UpdateMemberRoleAsync(item.Id, choice);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't change that role. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(TeamMemberListItemViewModel item)
    {
        if (!IsAdmin || Shell.Current is null)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert("Remove team member", $"Remove {item.Name} from this team?", "Remove", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _api.RemoveTeamMemberAsync(item.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't remove that team member. {ex.Message}";
        }
    }
}
