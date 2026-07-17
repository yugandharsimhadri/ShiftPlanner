using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Leave requests and shift-swap offers in one place — mirrors Web's
/// RequestsPanel folded into the Shift Planner. Three sections: what an Editor/Admin
/// needs to decide, open swaps anyone can claim, and the caller's own request history.</summary>
public partial class RequestsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private int? _myTeamMemberId;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    private bool canEdit;

    public ObservableCollection<RequestRowViewModel> PendingApproval { get; } = new();
    public ObservableCollection<RequestRowViewModel> OpenSwaps { get; } = new();
    public ObservableCollection<RequestRowViewModel> MyRequests { get; } = new();

    public bool HasPendingApproval => PendingApproval.Count > 0;
    public bool HasOpenSwaps => OpenSwaps.Count > 0;
    public bool HasNoMyRequests => MyRequests.Count == 0;

    public RequestsViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private static async Task RequestLeaveAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("leaverequestform");
        }
    }

    [RelayCommand]
    private static async Task OfferShiftAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("shiftswapform");
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsRefreshing = true;
            ErrorMessage = null;
            CanEdit = AppSettingsStore.CurrentTeamCanEdit;

            var members = await _api.GetTeamMembersAsync();
            _myTeamMemberId = members.FirstOrDefault(m =>
                !string.IsNullOrWhiteSpace(AppSettingsStore.MemberCode) &&
                string.Equals(m.Code, AppSettingsStore.MemberCode, StringComparison.OrdinalIgnoreCase))?.Id;

            var leave = await _api.GetLeaveRequestsAsync();
            var swaps = await _api.GetShiftSwapsAsync();

            Rebuild(leave, swaps);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load requests. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void Rebuild(List<LeaveRequest> leave, List<ShiftSwapRequest> swaps)
    {
        PendingApproval.Clear();
        OpenSwaps.Clear();
        MyRequests.Clear();

        if (CanEdit)
        {
            foreach (var l in leave.Where(l => l.Status == "Pending"))
            {
                PendingApproval.Add(new RequestRowViewModel
                {
                    Title = $"{l.MemberName} — leave",
                    Subtitle = string.IsNullOrWhiteSpace(l.Reason)
                        ? $"{l.StartDate:MMM d} – {l.EndDate:MMM d}"
                        : $"{l.StartDate:MMM d} – {l.EndDate:MMM d} · {l.Reason}",
                    StatusLabel = "Pending",
                    StatusColor = ColorOf("Pending", out var pb),
                    StatusBackground = pb,
                    PrimaryActionText = "Approve",
                    PrimaryActionCommand = new AsyncRelayCommand(() => DecideLeaveAsync(l.Id, approve: true)),
                    SecondaryActionText = "Reject",
                    SecondaryActionCommand = new AsyncRelayCommand(() => DecideLeaveAsync(l.Id, approve: false)),
                });
            }

            foreach (var s in swaps.Where(s => s.Status == "Claimed"))
            {
                PendingApproval.Add(new RequestRowViewModel
                {
                    Title = $"{s.OfferedByName} → {s.ClaimedByName}",
                    Subtitle = $"{s.Date:MMM d} · {s.ShiftCode}",
                    StatusLabel = "Claimed",
                    StatusColor = ColorOf("Pending", out var cb),
                    StatusBackground = cb,
                    PrimaryActionText = "Approve",
                    PrimaryActionCommand = new AsyncRelayCommand(() => DecideSwapAsync(s.Id, approve: true)),
                    SecondaryActionText = "Reject",
                    SecondaryActionCommand = new AsyncRelayCommand(() => DecideSwapAsync(s.Id, approve: false)),
                });
            }
        }

        foreach (var s in swaps.Where(s => s.Status == "Open" && s.OfferedByTeamMemberId != _myTeamMemberId))
        {
            OpenSwaps.Add(new RequestRowViewModel
            {
                Title = $"{s.OfferedByName} is offering",
                Subtitle = $"{s.Date:MMM d} · {s.ShiftCode}",
                StatusLabel = "Open",
                StatusColor = ColorOf("Open", out var ob),
                StatusBackground = ob,
                PrimaryActionText = "Claim",
                PrimaryActionCommand = new AsyncRelayCommand(() => ClaimSwapAsync(s.Id)),
            });
        }

        foreach (var l in leave.Where(l => string.Equals(l.MemberCode, AppSettingsStore.MemberCode, StringComparison.OrdinalIgnoreCase)))
        {
            MyRequests.Add(new RequestRowViewModel
            {
                Title = $"Leave: {l.StartDate:MMM d} – {l.EndDate:MMM d}",
                Subtitle = string.IsNullOrWhiteSpace(l.Reason) ? "No reason given" : l.Reason,
                StatusLabel = l.Status,
                StatusColor = ColorOf(l.Status, out var lb),
                StatusBackground = lb,
                PrimaryActionText = l.Status == "Pending" ? "Cancel" : null,
                PrimaryActionCommand = l.Status == "Pending" ? new AsyncRelayCommand(() => CancelLeaveAsync(l.Id)) : null,
            });
        }

        foreach (var s in swaps.Where(s => s.OfferedByTeamMemberId == _myTeamMemberId || s.ClaimedByTeamMemberId == _myTeamMemberId))
        {
            var isMine = s.OfferedByTeamMemberId == _myTeamMemberId;
            MyRequests.Add(new RequestRowViewModel
            {
                Title = $"Swap: {s.Date:MMM d} · {s.ShiftCode}",
                Subtitle = isMine ? "You offered this shift" : $"You claimed from {s.OfferedByName}",
                StatusLabel = s.Status,
                StatusColor = ColorOf(s.Status, out var sb),
                StatusBackground = sb,
                PrimaryActionText = isMine && s.Status == "Open" ? "Cancel" : null,
                PrimaryActionCommand = isMine && s.Status == "Open" ? new AsyncRelayCommand(() => CancelSwapAsync(s.Id)) : null,
            });
        }

        OnPropertyChanged(nameof(HasPendingApproval));
        OnPropertyChanged(nameof(HasOpenSwaps));
        OnPropertyChanged(nameof(HasNoMyRequests));
    }

    private async Task DecideLeaveAsync(int id, bool approve)
    {
        try
        {
            if (approve) await _api.ApproveLeaveRequestAsync(id);
            else await _api.RejectLeaveRequestAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't decide the request. {ex.Message}";
        }
    }

    private async Task DecideSwapAsync(int id, bool approve)
    {
        try
        {
            if (approve) await _api.ApproveShiftSwapAsync(id);
            else await _api.RejectShiftSwapAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't decide the swap. {ex.Message}";
        }
    }

    private async Task ClaimSwapAsync(int id)
    {
        try
        {
            await _api.ClaimShiftSwapAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't claim that shift. {ex.Message}";
        }
    }

    private async Task CancelLeaveAsync(int id)
    {
        try
        {
            await _api.CancelLeaveRequestAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't cancel the request. {ex.Message}";
        }
    }

    private async Task CancelSwapAsync(int id)
    {
        try
        {
            await _api.CancelShiftSwapAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't cancel the offer. {ex.Message}";
        }
    }

    private static Color ColorOf(string status, out Color background)
    {
        var resources = Application.Current!.Resources;
        switch (status)
        {
            case "Approved":
                background = (Color)resources["AccentSoftLight"];
                return (Color)resources["AccentLight"];
            case "Rejected":
            case "Cancelled":
                background = (Color)resources["ShiftLeaveBg"];
                return (Color)resources["ShiftLeaveFg"];
            case "Pending":
                background = (Color)resources["ShiftMorningBg"];
                return (Color)resources["ShiftMorningFg"];
            default: // Open, Claimed
                background = (Color)resources["ShiftOffBg"];
                return (Color)resources["ShiftOffFg"];
        }
    }
}
