using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One of the caller's own upcoming assigned shifts, offerable via
/// POST /api/shift-swaps.</summary>
public sealed class MyUpcomingShiftOption
{
    public required DateOnly Date { get; init; }
    public required string ShiftCode { get; init; }
    public string Label => $"{Date:ddd, MMM d} — {ShiftCode}";
}

/// <summary>POST /api/shift-swaps — offer one of the caller's own upcoming shifts for
/// someone else to take. A one-directional give-away, not a mutual trade.</summary>
public partial class ShiftSwapFormViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanSubmit => !IsSaving && SelectedShift is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private MyUpcomingShiftOption? selectedShift;

    [ObservableProperty]
    private TeamMember? selectedTarget;

    [ObservableProperty]
    private bool hasNoUpcomingShifts;

    public ObservableCollection<MyUpcomingShiftOption> UpcomingShifts { get; } = new();
    public ObservableCollection<TeamMember> TeamMembersForPicker { get; } = new();

    public ShiftSwapFormViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var members = await _api.GetTeamMembersAsync();
            var myCode = AppSettingsStore.MemberCode;
            var me = members.FirstOrDefault(m =>
                !string.IsNullOrWhiteSpace(myCode) && string.Equals(m.Code, myCode, StringComparison.OrdinalIgnoreCase));

            TeamMembersForPicker.Clear();
            foreach (var m in members.Where(m => m.Status == "Active" && m.Id != me?.Id).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                TeamMembersForPicker.Add(m);
            }

            UpcomingShifts.Clear();
            if (me is not null)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var thisMonth = await _api.GetRosterMonthAsync(today.Year, today.Month);
                var nextMonthDate = today.AddMonths(1);
                var nextMonth = await _api.GetRosterMonthAsync(nextMonthDate.Year, nextMonthDate.Month);

                var options = thisMonth.Entries.Concat(nextMonth.Entries)
                    .Where(e => e.TeamMemberId == me.Id && e.Date >= today && !string.IsNullOrWhiteSpace(e.ShiftCode))
                    .OrderBy(e => e.Date)
                    .Select(e => new MyUpcomingShiftOption { Date = e.Date, ShiftCode = e.ShiftCode! });

                foreach (var option in options)
                {
                    UpcomingShifts.Add(option);
                }
            }

            HasNoUpcomingShifts = UpcomingShifts.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load your upcoming shifts. {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Shell.Current is null || SelectedShift is null)
        {
            return;
        }

        try
        {
            IsSaving = true;
            ErrorMessage = null;
            await _api.CreateShiftSwapAsync(SelectedShift.Date, SelectedShift.ShiftCode, SelectedTarget?.Id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't offer that shift. {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
