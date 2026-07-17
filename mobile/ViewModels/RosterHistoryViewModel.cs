using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>GET /api/roster/history?year=&amp;month= — a lightweight audit log of
/// roster-cell changes for the month, reached from the Roster toolbar's "History" button.</summary>
[QueryProperty(nameof(YearText), "year")]
[QueryProperty(nameof(MonthText), "month")]
public partial class RosterHistoryViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;

    [ObservableProperty]
    private string yearText = string.Empty;

    [ObservableProperty]
    private string monthText = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<RosterEntryHistoryRow> Rows { get; } = new();

    public RosterHistoryViewModel(ApiClient api)
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
            IsRefreshing = true;
            ErrorMessage = null;

            var rows = await _api.GetRosterHistoryAsync(_year, _month);
            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load history. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
