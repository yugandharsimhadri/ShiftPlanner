using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Who's actually free right now — separate from the planned roster. Mirrors
/// Web's Live page: a self-toggle plus the whole team's live status. Refreshes on tab
/// appear rather than a background timer (simpler, battery friendlier on mobile).</summary>
public partial class LiveViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MySelfLabel))]
    [NotifyPropertyChangedFor(nameof(MySelfHint))]
    [NotifyPropertyChangedFor(nameof(MySelfButtonText))]
    private bool myIsAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleSelf))]
    [NotifyPropertyChangedFor(nameof(MySelfButtonText))]
    private bool isTogglingSelf;

    [ObservableProperty]
    private bool hasMyRow;

    public string MySelfLabel => MyIsAvailable ? "You're available" : "You're not available";
    public string MySelfHint => MyIsAvailable ? "Team members can see you as free right now." : "Flip this on when you can take work.";
    public string MySelfButtonText => IsTogglingSelf ? "Updating…" : MyIsAvailable ? "Go unavailable" : "I'm available";
    public bool CanToggleSelf => !IsTogglingSelf;

    public ObservableCollection<LiveMemberRowViewModel> Members { get; } = new();

    public LiveViewModel(ApiClient api)
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

            var members = await _api.GetTeamAvailabilityAsync();
            var myCode = AppSettingsStore.MemberCode;
            var mine = members.FirstOrDefault(m => !string.IsNullOrWhiteSpace(myCode) && string.Equals(m.Code, myCode, StringComparison.OrdinalIgnoreCase));
            HasMyRow = mine is not null;
            MyIsAvailable = mine?.IsAvailable ?? false;

            var now = DateTimeOffset.UtcNow;
            var ordered = members
                .OrderByDescending(m => m.IsAvailable)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);

            Members.Clear();
            foreach (var m in ordered)
            {
                Members.Add(new LiveMemberRowViewModel
                {
                    TeamMemberId = m.TeamMemberId,
                    Code = m.Code,
                    Name = m.Name,
                    TrackName = m.TrackName,
                    IsAvailable = m.IsAvailable,
                    StatusLabel = m.IsAvailable ? $"Available{AvailableForSuffix(m.AvailableSince, now)}" : "Not available",
                    StatusColor = m.IsAvailable
                        ? (Color)Application.Current!.Resources["AccentLight"]
                        : (Color)Application.Current!.Resources["ShiftOffFg"],
                    StatusBackground = m.IsAvailable
                        ? (Color)Application.Current!.Resources["AccentSoftLight"]
                        : (Color)Application.Current!.Resources["ShiftOffBg"],
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load availability. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ToggleSelfAsync()
    {
        try
        {
            IsTogglingSelf = true;
            ErrorMessage = null;
            await _api.UpdateMyAvailabilityAsync(!MyIsAvailable);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't update your availability. {ex.Message}";
        }
        finally
        {
            IsTogglingSelf = false;
        }
    }

    private static string AvailableForSuffix(DateTimeOffset? since, DateTimeOffset now)
    {
        if (since is not { } s) return string.Empty;
        var minutes = Math.Max(0, (int)Math.Round((now - s).TotalMinutes));
        if (minutes < 1) return " · just now";
        if (minutes < 60) return $" · {minutes}m";
        var hours = minutes / 60;
        var remainder = minutes % 60;
        return remainder > 0 ? $" · {hours}h {remainder}m" : $" · {hours}h";
    }
}
