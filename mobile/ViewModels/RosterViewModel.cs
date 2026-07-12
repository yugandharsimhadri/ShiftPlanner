using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>
/// The team's roster, one day at a time — replaces the old separate "My Shifts" (personal)
/// and "Today" (manager) tabs, which were always the same data viewed two ways. Everyone sees
/// it; only Editor/Admin can tap a chip to change it (see AppSettingsStore.CurrentTeamCanEdit).
/// </summary>
public partial class RosterViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private RosterResponse? _monthData;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private string dateLabel = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    private bool showJustMine;

    [ObservableProperty]
    private bool canEdit;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<RosterTrackGroup> Groups { get; } = new();

    public RosterViewModel(ApiClient api)
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

            _monthData = await _api.GetRosterMonthAsync(_selectedDate.Year, _selectedDate.Month);
            Rebuild();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load the roster. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task PreviousDayAsync() => GoToDateAsync(_selectedDate.AddDays(-1));

    [RelayCommand]
    private Task NextDayAsync() => GoToDateAsync(_selectedDate.AddDays(1));

    [RelayCommand]
    private Task GoToTodayAsync() => GoToDateAsync(DateOnly.FromDateTime(DateTime.Today));

    [RelayCommand]
    private void ToggleJustMine()
    {
        ShowJustMine = !ShowJustMine;
        Rebuild();
    }

    private async Task GoToDateAsync(DateOnly date)
    {
        var monthChanged = date.Year != _selectedDate.Year || date.Month != _selectedDate.Month;
        _selectedDate = date;

        if (monthChanged || _monthData is null)
        {
            await LoadAsync();
        }
        else
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        DateLabel = _selectedDate.ToString("dddd, MMM d");
        Groups.Clear();

        if (_monthData is null)
        {
            return;
        }

        var myCode = AppSettingsStore.EmployeeCode;
        var entriesForDay = _monthData.Entries.Where(e => e.Date == _selectedDate).ToDictionary(e => e.EmployeeId);
        var shiftsByCode = _monthData.ShiftTypes.ToDictionary(s => s.Code);

        var employees = _monthData.Employees.AsEnumerable();
        if (ShowJustMine && !string.IsNullOrWhiteSpace(myCode))
        {
            employees = employees.Where(e => string.Equals(e.Code, myCode, StringComparison.OrdinalIgnoreCase));
        }

        var rows = employees
            .Select(emp =>
            {
                entriesForDay.TryGetValue(emp.Id, out var entry);
                var shiftCode = entry?.ShiftCode;
                shiftsByCode.TryGetValue(shiftCode ?? string.Empty, out var shift);
                var (fg, bg) = ShiftStyle.Resolve(shiftCode, shift?.Color);

                return new
                {
                    TrackName = emp.Track?.Name ?? "Unassigned",
                    Row = new RosterEmployeeRowViewModel
                    {
                        EmployeeId = emp.Id,
                        EmployeeCode = emp.Code,
                        EmployeeName = emp.Name,
                        ShiftCode = shiftCode,
                        ShiftLabel = shift?.Name ?? shiftCode ?? "Off",
                        HasShift = !string.IsNullOrWhiteSpace(shiftCode),
                        ChipForeground = fg,
                        ChipBackground = bg,
                        IsMe = !string.IsNullOrWhiteSpace(myCode) && string.Equals(emp.Code, myCode, StringComparison.OrdinalIgnoreCase),
                    }
                };
            })
            .GroupBy(x => x.TrackName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RosterTrackGroup(g.Key, g.Select(x => x.Row).OrderBy(r => r.EmployeeName, StringComparer.OrdinalIgnoreCase)));

        foreach (var group in rows)
        {
            Groups.Add(group);
        }
    }

    [RelayCommand]
    private async Task TapShiftAsync(RosterEmployeeRowViewModel row)
    {
        if (!CanEdit || Shell.Current is null || _monthData is null)
        {
            return;
        }

        var options = _monthData.ShiftTypes.Select(s => s.Code).ToArray();
        var choice = await Shell.Current.DisplayActionSheet(
            $"Assign shift — {row.EmployeeName}, {DateLabel}", "Cancel", "Clear", options);

        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
        {
            return;
        }

        var shiftCode = choice == "Clear" ? null : choice;

        try
        {
            await _api.UpsertRosterEntryAsync(row.EmployeeId, _selectedDate, shiftCode);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't update the shift. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyForwardAsync()
    {
        if (!CanEdit || Shell.Current is null)
        {
            return;
        }

        var sourceMonth = _selectedDate.AddMonths(-1);
        var targetMonth = _selectedDate;

        var confirmed = await Shell.Current.DisplayAlert(
            "Copy month forward",
            $"Copy {sourceMonth:MMMM yyyy} onto {targetMonth:MMMM yyyy}, matching each entry to the same weekday? Employees marked inactive are skipped.",
            "Copy", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            IsRefreshing = true;
            var result = await _api.CopyForwardAsync(new CopyForwardRequestBody
            {
                SourceYear = sourceMonth.Year,
                SourceMonth = sourceMonth.Month,
                TargetYear = targetMonth.Year,
                TargetMonth = targetMonth.Month,
                Pattern = "weekday",
                SkipInactive = true,
            });

            await LoadAsync();

            var summary = result.Flagged.Count == 0
                ? $"Copied {result.CopiedCount} entries."
                : $"Copied {result.CopiedCount} entries. {result.Flagged.Count} need a look: " +
                  string.Join("; ", result.Flagged.Take(5).Select(f => $"{f.EmployeeName} {f.Date:MMM d} ({f.Reason})"));
            await Shell.Current.DisplayAlert("Copy complete", summary, "OK");
        }
        catch (Exception ex)
        {
            var message = ex is ApiException apiEx ? apiEx.Message : $"Couldn't copy the month forward. {ex.Message}";
            await Shell.Current.DisplayAlert("Couldn't copy month", message, "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet("Export this month", "Cancel", null, "Excel (.xlsx)", "CSV");
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
        {
            return;
        }

        var kind = choice.StartsWith("Excel", StringComparison.OrdinalIgnoreCase) ? "excel" : "csv";
        var ext = kind == "excel" ? "xlsx" : "csv";

        try
        {
            var bytes = await _api.DownloadExportAsync(kind, _selectedDate.Year, _selectedDate.Month);
            var fileName = $"roster-{_selectedDate:yyyy-MM}.{ext}";
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(path, bytes);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share roster export",
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            var message = ex is ApiException apiEx ? apiEx.Message : $"Couldn't export the roster. {ex.Message}";
            await Shell.Current.DisplayAlert("Export failed", message, "OK");
        }
    }
}
