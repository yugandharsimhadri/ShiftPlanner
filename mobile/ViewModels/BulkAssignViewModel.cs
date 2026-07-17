using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>POST /api/roster/bulk-entry — assign one shift (or clear) to many members
/// across a date range at once. Reached from the Roster toolbar's "Bulk assign" button
/// for the month currently being viewed.</summary>
[QueryProperty(nameof(YearText), "year")]
[QueryProperty(nameof(MonthText), "month")]
public partial class BulkAssignViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;

    [ObservableProperty]
    private string yearText = string.Empty;

    [ObservableProperty]
    private string monthText = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool isSaving;

    public bool CanSubmit => !IsSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today;

    [ObservableProperty]
    private string? selectedShiftCode;

    public ObservableCollection<SelectableTeamMember> Members { get; } = new();
    public ObservableCollection<string> ShiftCodeOptions { get; } = new();

    public BulkAssignViewModel(ApiClient api)
    {
        _api = api;
    }

    partial void OnYearTextChanged(string value) => _year = int.TryParse(value, out var y) && y > 0 ? y : _year;
    partial void OnMonthTextChanged(string value) => _month = int.TryParse(value, out var m) && m is >= 1 and <= 12 ? m : _month;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var members = await _api.GetTeamMembersAsync();
            Members.Clear();
            foreach (var m in members.Where(m => m.Status == "Active").OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                Members.Add(new SelectableTeamMember { Member = m });
            }

            var shiftTypes = await _api.GetShiftTypesAsync();
            ShiftCodeOptions.Clear();
            ShiftCodeOptions.Add("Clear (no shift)");
            foreach (var s in shiftTypes.OrderBy(s => s.Code, StringComparer.OrdinalIgnoreCase))
            {
                ShiftCodeOptions.Add(s.Code);
            }

            StartDate = new DateTime(_year, _month, 1);
            EndDate = new DateTime(_year, _month, DateTime.DaysInMonth(_year, _month));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load team members. {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        var selectedIds = Members.Where(m => m.IsSelected).Select(m => m.Member.Id).ToList();
        if (selectedIds.Count == 0)
        {
            ErrorMessage = "Select at least one team member.";
            return;
        }

        if (EndDate < StartDate)
        {
            ErrorMessage = "End date can't be before the start date.";
            return;
        }

        var dates = new List<DateOnly>();
        for (var d = DateOnly.FromDateTime(StartDate); d <= DateOnly.FromDateTime(EndDate); d = d.AddDays(1))
        {
            dates.Add(d);
        }

        var shiftCode = SelectedShiftCode is null || SelectedShiftCode.StartsWith("Clear", StringComparison.Ordinal)
            ? null
            : SelectedShiftCode;

        try
        {
            IsSaving = true;
            ErrorMessage = null;
            var result = await _api.BulkAssignAsync(selectedIds, dates, shiftCode);
            var summary = result.Errors.Count == 0
                ? $"Updated {result.UpdatedCount} entries."
                : $"Updated {result.UpdatedCount} entries. {result.Errors.Count} skipped: {string.Join("; ", result.Errors.Take(5))}";
            await Shell.Current.DisplayAlert("Bulk assign complete", summary, "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't bulk-assign. {ex.Message}";
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
