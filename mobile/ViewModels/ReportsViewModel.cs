using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>GET /api/reports/utilization?start=&amp;end= — how much each active member
/// worked in a date range, plus their comp-off standing. Reached from Settings'
/// "View reports" link (Admin-only, since Settings gates the whole Managers/Reports area).</summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private DateTime startDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime endDate = DateTime.Today;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<UtilizationRow> Rows { get; } = new();

    public ReportsViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (EndDate < StartDate)
        {
            ErrorMessage = "End date can't be before the start date.";
            return;
        }

        try
        {
            IsRefreshing = true;
            ErrorMessage = null;

            var rows = await _api.GetUtilizationReportAsync(DateOnly.FromDateTime(StartDate), DateOnly.FromDateTime(EndDate));
            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load the report. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
