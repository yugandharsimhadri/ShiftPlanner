using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Team member list — visible to everyone, but invite/role-change/link/remove are
/// Admin-only, mirroring the server's RequireTeamAdmin() on those endpoints.</summary>
public partial class TeamViewModel : ObservableObject
{
    private static readonly string[] Roles = { "Viewer", "Editor", "Admin" };

    private readonly ApiClient _api;
    private List<Employee> _employees = new();

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isAdmin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty] private string inviteEmail = string.Empty;
    [ObservableProperty] private string inviteRole = "Viewer";

    public List<string> RoleChoices { get; } = Roles.ToList();
    public ObservableCollection<MemberItemViewModel> Members { get; } = new();

    public TeamViewModel(ApiClient api)
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
            IsAdmin = AppSettingsStore.CurrentTeamIsAdmin;

            var members = await _api.GetMembersAsync();
            _employees = await _api.GetEmployeesAsync();
            var employeesById = _employees.ToDictionary(e => e.Id);

            Members.Clear();
            foreach (var m in members.OrderBy(m => m.Email, StringComparer.OrdinalIgnoreCase))
            {
                var linkedLabel = m.EmployeeId is { } empId && employeesById.TryGetValue(empId, out var emp)
                    ? $"Linked — {emp.Code}"
                    : "Not linked";
                Members.Add(new MemberItemViewModel { Membership = m, LinkedLabel = linkedLabel });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load the team. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task InviteAsync()
    {
        if (!IsAdmin || string.IsNullOrWhiteSpace(InviteEmail))
        {
            return;
        }

        try
        {
            await _api.AddMemberAsync(InviteEmail.Trim(), InviteRole);
            InviteEmail = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't invite that person. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ChangeRoleAsync(MemberItemViewModel member)
    {
        if (!IsAdmin || Shell.Current is null)
        {
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet($"Role for {member.Email}", "Cancel", null, Roles);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel" || choice == member.Role)
        {
            return;
        }

        try
        {
            await _api.UpdateMemberRoleAsync(member.Id, choice);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't change that role. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LinkEmployeeAsync(MemberItemViewModel member)
    {
        if (!IsAdmin || Shell.Current is null)
        {
            return;
        }

        var codes = _employees.Select(e => $"{e.Code} — {e.Name}").ToArray();
        var choice = await Shell.Current.DisplayActionSheet($"Link {member.Email} to an employee", "Cancel", "Unlink", codes);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
        {
            return;
        }

        Guid? employeeId = null;
        if (choice != "Unlink")
        {
            var code = choice.Split(" — ")[0];
            employeeId = _employees.FirstOrDefault(e => e.Code == code)?.Id;
        }

        try
        {
            await _api.LinkMemberEmployeeAsync(member.Id, employeeId);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't update that link. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(MemberItemViewModel member)
    {
        if (!IsAdmin || Shell.Current is null)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert("Remove member", $"Remove {member.Email} from this team?", "Remove", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _api.RemoveMemberAsync(member.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't remove that member. {ex.Message}";
        }
    }
}
