using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>POST /api/roster/apply-pattern — a per-weekday shift pattern applied across
/// a whole month for the selected members. A scoped-down alternative to a full
/// rotation/cycle engine. Reached from the Roster toolbar's "Apply pattern" button.</summary>
[QueryProperty(nameof(YearText), "year")]
[QueryProperty(nameof(MonthText), "month")]
public partial class ApplyPatternViewModel : ObservableObject
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
    private bool skipInactive = true;

    public ObservableCollection<SelectableTeamMember> Members { get; } = new();
    public ObservableCollection<string> ShiftCodeOptions { get; } = new();
    public ObservableCollection<WeekdayPatternRow> WeekdayRows { get; } = new();

    public ApplyPatternViewModel(ApiClient api)
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
            ShiftCodeOptions.Add("(none)");
            foreach (var s in shiftTypes.OrderBy(s => s.Code, StringComparer.OrdinalIgnoreCase))
            {
                ShiftCodeOptions.Add(s.Code);
            }

            if (WeekdayRows.Count == 0)
            {
                var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
                foreach (var day in days)
                {
                    WeekdayRows.Add(new WeekdayPatternRow { Day = day, DayLabel = day.ToString() });
                }
            }
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

        var pattern = WeekdayRows
            .Where(r => !string.IsNullOrWhiteSpace(r.SelectedShiftCode) && r.SelectedShiftCode != "(none)")
            .ToDictionary(r => r.Day, r => (string?)r.SelectedShiftCode);

        if (pattern.Count == 0)
        {
            ErrorMessage = "Pick a shift for at least one day of the week.";
            return;
        }

        try
        {
            IsSaving = true;
            ErrorMessage = null;
            var body = new ApplyPatternBody
            {
                Year = _year,
                Month = _month,
                TeamMemberIds = selectedIds,
                WeeklyPattern = pattern,
                SkipInactive = SkipInactive,
            };
            var result = await _api.ApplyPatternAsync(body);
            var summary = result.Errors.Count == 0
                ? $"Updated {result.UpdatedCount} entries."
                : $"Updated {result.UpdatedCount} entries. {result.Errors.Count} skipped: {string.Join("; ", result.Errors.Take(5))}";
            await Shell.Current.DisplayAlert("Pattern applied", summary, "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't apply the pattern. {ex.Message}";
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
