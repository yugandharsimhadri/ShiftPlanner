using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Team settings (Lead/Co-Lead, auto-approve), tracks/subtracks, and shift
/// types — mirrors Web's Settings page, gated to Editor/Admin (team settings and Lead/
/// Co-Lead changes require Admin specifically; Viewers see everything read-only).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool canEdit;

    // Team settings (name/off-days/auto-approve) and Lead/Co-Lead are Admin-only
    // server-side (RequireTeamAdmin) — stricter than the Editor-level CanEdit that
    // gates Tracks/Shift-types.
    [ObservableProperty]
    private bool isAdmin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = new();
    public ObservableCollection<ShiftTypeItemViewModel> ShiftTypes { get; } = new();
    public ObservableCollection<TeamMember> TeamMembersForPicker { get; } = new();

    [ObservableProperty] private string newTrackName = string.Empty;
    [ObservableProperty] private string newTrackColor = "#2F7D6B";

    private TeamSettings? _teamSettings;

    [ObservableProperty] private TeamMember? selectedLead;
    [ObservableProperty] private TeamMember? selectedCoLead;
    [ObservableProperty] private bool autoApproveLeaveRequests = true;
    [ObservableProperty] private bool autoApproveShiftSwaps = true;
    [ObservableProperty] private bool isTeamSettingsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTeamSettingsStatus))]
    private string? teamSettingsStatus;

    public bool HasTeamSettingsStatus => !string.IsNullOrWhiteSpace(TeamSettingsStatus);

    [ObservableProperty] private TrackItemViewModel? subtrackParentTrack;
    [ObservableProperty] private string newSubtrackName = string.Empty;

    [ObservableProperty] private string newShiftCode = string.Empty;
    [ObservableProperty] private string newShiftName = string.Empty;
    [ObservableProperty] private string newShiftColor = "#2F7D6B";
    [ObservableProperty] private bool newShiftOvernight;

    public ObservableCollection<ManagerAssignment> Managers { get; } = new();
    public ObservableCollection<PersonSearchResult> ManagerCandidates { get; } = new();

    [ObservableProperty] private string managerSearchPhone = string.Empty;
    [ObservableProperty] private bool isManagerSearchBusy;

    public SettingsViewModel(ApiClient api)
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

            var tracks = await _api.GetTracksAsync();
            Tracks.Clear();
            foreach (var track in tracks.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                Tracks.Add(new TrackItemViewModel { Track = track });
            }
            SubtrackParentTrack = Tracks.FirstOrDefault();

            var shiftTypes = await _api.GetShiftTypesAsync();
            ShiftTypes.Clear();
            foreach (var shiftType in shiftTypes.OrderBy(s => s.Code, StringComparer.OrdinalIgnoreCase))
            {
                ShiftTypes.Add(new ShiftTypeItemViewModel { ShiftType = shiftType });
            }

            var members = await _api.GetTeamMembersAsync();
            TeamMembersForPicker.Clear();
            foreach (var member in members.Where(m => m.Status == "Active").OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                TeamMembersForPicker.Add(member);
            }
            SelectedLead = TeamMembersForPicker.FirstOrDefault(m => m.IsTeamLead);
            SelectedCoLead = TeamMembersForPicker.FirstOrDefault(m => m.IsCoLead);

            _teamSettings = await _api.GetTeamSettingsAsync();
            AutoApproveLeaveRequests = _teamSettings.AutoApproveLeaveRequests;
            AutoApproveShiftSwaps = _teamSettings.AutoApproveShiftSwaps;

            if (IsAdmin)
            {
                var managers = await _api.GetManagersAsync();
                Managers.Clear();
                foreach (var m in managers)
                {
                    Managers.Add(m);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load settings. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task UpdateLeadAsync()
    {
        if (!IsAdmin || SelectedLead is null || SelectedLead.IsTeamLead)
        {
            return;
        }

        try
        {
            IsTeamSettingsBusy = true;
            TeamSettingsStatus = null;
            await _api.TransferLeadAsync(SelectedLead.Id);
            TeamSettingsStatus = $"{SelectedLead.Name} is now the team lead.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't transfer the lead. {ex.Message}";
        }
        finally
        {
            IsTeamSettingsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpdateCoLeadAsync()
    {
        if (!IsAdmin)
        {
            return;
        }

        try
        {
            IsTeamSettingsBusy = true;
            TeamSettingsStatus = null;

            var previousCoLead = TeamMembersForPicker.FirstOrDefault(m => m.IsCoLead && m.Id != SelectedCoLead?.Id);
            if (previousCoLead is not null)
            {
                await _api.SetCoLeadAsync(previousCoLead.Id, false);
            }

            if (SelectedCoLead is not null && !SelectedCoLead.IsCoLead)
            {
                await _api.SetCoLeadAsync(SelectedCoLead.Id, true);
            }

            TeamSettingsStatus = SelectedCoLead is null ? "Co-lead cleared." : $"{SelectedCoLead.Name} is now the co-lead.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't update the co-lead. {ex.Message}";
        }
        finally
        {
            IsTeamSettingsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAutoApproveAsync()
    {
        if (!IsAdmin || _teamSettings is null)
        {
            return;
        }

        try
        {
            IsTeamSettingsBusy = true;
            TeamSettingsStatus = null;
            _teamSettings = await _api.UpdateTeamSettingsAsync(new UpdateTeamSettingsRequest
            {
                Name = _teamSettings.Name,
                OrgName = _teamSettings.OrgName,
                TeamStrength = _teamSettings.TeamStrength,
                ShiftsCovered = _teamSettings.ShiftsCovered,
                DefaultOffDays = _teamSettings.DefaultOffDays,
                CompOffsEnabled = _teamSettings.CompOffsEnabled,
                AutoApproveLeaveRequests = AutoApproveLeaveRequests,
                AutoApproveShiftSwaps = AutoApproveShiftSwaps,
            });
            TeamSettingsStatus = "Saved.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't save team settings. {ex.Message}";
        }
        finally
        {
            IsTeamSettingsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddTrackAsync()
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(NewTrackName))
        {
            return;
        }

        try
        {
            await _api.CreateTrackAsync(new TrackInput
            {
                Name = NewTrackName.Trim(),
                Color = string.IsNullOrWhiteSpace(NewTrackColor) ? "#2F7D6B" : NewTrackColor.Trim(),
            });
            NewTrackName = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't add the track. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteTrackAsync(TrackItemViewModel track)
    {
        if (!CanEdit || Shell.Current is null)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert("Delete track",
            $"Delete {track.Name}? This fails if employees are still assigned to it.", "Delete", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _api.DeleteTrackAsync(track.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't delete the track. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddSubtrackAsync()
    {
        if (!CanEdit || SubtrackParentTrack is null || string.IsNullOrWhiteSpace(NewSubtrackName))
        {
            return;
        }

        try
        {
            await _api.CreateSubtrackAsync(new SubtrackInput { TrackId = SubtrackParentTrack.Id, Name = NewSubtrackName.Trim() });
            NewSubtrackName = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't add the subtrack. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ManageSubtracksAsync(TrackItemViewModel track)
    {
        if (!CanEdit || Shell.Current is null || track.Track.Subtracks.Count == 0)
        {
            return;
        }

        var names = track.Track.Subtracks.Select(s => s.Name).ToArray();
        var choice = await Shell.Current.DisplayActionSheet($"Remove a subtrack from {track.Name}", "Cancel", null, names);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
        {
            return;
        }

        var target = track.Track.Subtracks.FirstOrDefault(s => s.Name == choice);
        if (target is null)
        {
            return;
        }

        try
        {
            await _api.DeleteSubtrackAsync(target.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't remove the subtrack. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddShiftTypeAsync()
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(NewShiftCode) || string.IsNullOrWhiteSpace(NewShiftName))
        {
            return;
        }

        try
        {
            await _api.CreateShiftTypeAsync(new ShiftTypeInput
            {
                Code = NewShiftCode.Trim(),
                Name = NewShiftName.Trim(),
                Color = string.IsNullOrWhiteSpace(NewShiftColor) ? "#2F7D6B" : NewShiftColor.Trim(),
                IsOvernight = NewShiftOvernight,
            });
            NewShiftCode = string.Empty;
            NewShiftName = string.Empty;
            NewShiftOvernight = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't add the shift type. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteShiftTypeAsync(ShiftTypeItemViewModel shiftType)
    {
        if (!CanEdit || Shell.Current is null)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert("Delete shift type",
            $"Delete {shiftType.Code} — {shiftType.Name}?", "Delete", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _api.DeleteShiftTypeAsync(shiftType.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't delete the shift type. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SearchManagerCandidatesAsync()
    {
        if (!IsAdmin || string.IsNullOrWhiteSpace(ManagerSearchPhone))
        {
            return;
        }

        try
        {
            IsManagerSearchBusy = true;
            ErrorMessage = null;
            var results = await _api.SearchManagerCandidatesAsync(ManagerSearchPhone.Trim());
            ManagerCandidates.Clear();
            foreach (var r in results)
            {
                ManagerCandidates.Add(r);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't search for managers. {ex.Message}";
        }
        finally
        {
            IsManagerSearchBusy = false;
        }
    }

    [RelayCommand]
    private async Task GrantManagerAsync(PersonSearchResult candidate)
    {
        if (!IsAdmin)
        {
            return;
        }

        try
        {
            await _api.GrantManagerAsync(candidate.Id);
            ManagerCandidates.Clear();
            ManagerSearchPhone = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't grant manager access. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RevokeManagerAsync(ManagerAssignment assignment)
    {
        if (!IsAdmin)
        {
            return;
        }

        try
        {
            await _api.RevokeManagerAsync(assignment.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't revoke manager access. {ex.Message}";
        }
    }

    [RelayCommand]
    private static async Task ViewReportsAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("reports");
        }
    }
}
