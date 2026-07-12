using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Tracks/subtracks and shift types — the same CRUD Web's Settings page offers,
/// gated to Editor/Admin. Viewers see the same lists read-only.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = new();
    public ObservableCollection<ShiftTypeItemViewModel> ShiftTypes { get; } = new();

    [ObservableProperty] private string newTrackName = string.Empty;
    [ObservableProperty] private string newTrackLead = string.Empty;
    [ObservableProperty] private string newTrackColor = "#2F7D6B";

    [ObservableProperty] private TrackItemViewModel? subtrackParentTrack;
    [ObservableProperty] private string newSubtrackName = string.Empty;

    [ObservableProperty] private string newShiftCode = string.Empty;
    [ObservableProperty] private string newShiftName = string.Empty;
    [ObservableProperty] private string newShiftColor = "#2F7D6B";
    [ObservableProperty] private bool newShiftOvernight;

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
                LeadName = string.IsNullOrWhiteSpace(NewTrackLead) ? null : NewTrackLead.Trim(),
                Color = string.IsNullOrWhiteSpace(NewTrackColor) ? "#2F7D6B" : NewTrackColor.Trim(),
            });
            NewTrackName = string.Empty;
            NewTrackLead = string.Empty;
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
}
