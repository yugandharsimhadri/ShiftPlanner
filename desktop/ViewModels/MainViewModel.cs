using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Desktop.Data;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ShiftPlannerContext db = new();

    [ObservableProperty] private DateOnly monthStart;
    [ObservableProperty] private string monthLabel = "";
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private Track? selectedTrackFilter;
    [ObservableProperty] private string statusText = "";

    public ObservableCollection<EmployeeRowViewModel> Rows { get; } = new();
    public ObservableCollection<DateOnly> Days { get; } = new();
    public ObservableCollection<Track> Tracks { get; } = new();
    public ObservableCollection<Track> TrackFilterOptions { get; } = new();
    public List<ShiftType> ShiftTypes { get; private set; } = new();

    public ICollectionView RowsView { get; }

    public event EventHandler? MonthColumnsChanged;

    public MainViewModel()
    {
        db.EnsureSeeded();
        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(EmployeeRowViewModel.TrackName)));

        MonthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        LoadReferenceData();
        LoadMonth();
    }

    public ShiftPlannerContext Db => db;

    public void LoadReferenceData()
    {
        Tracks.Clear();
        foreach (var t in db.Tracks.OrderBy(t => t.Name)) Tracks.Add(t);

        ShiftTypes = db.ShiftTypes.OrderBy(s => s.SortOrder).ToList();

        TrackFilterOptions.Clear();
        TrackFilterOptions.Add(new Track { Id = 0, Name = "All tracks" });
        foreach (var t in Tracks) TrackFilterOptions.Add(t);
        SelectedTrackFilter = TrackFilterOptions[0];
    }

    partial void OnMonthStartChanged(DateOnly value) => LoadMonth();
    partial void OnSearchTextChanged(string value) => LoadMonth();
    partial void OnSelectedTrackFilterChanged(Track? value) => LoadMonth();

    public void LoadMonth()
    {
        MonthLabel = MonthStart.ToString("MMM yyyy");

        Days.Clear();
        for (var d = MonthStart; d.Month == MonthStart.Month; d = d.AddDays(1))
            Days.Add(d);

        var holidays = db.Holidays.Select(h => h.Date).ToHashSet();

        var employeesQuery = db.Employees
            .Include(e => e.Track)
            .Include(e => e.Subtrack)
            .Where(e => e.Status == EmployeeStatus.Active)
            .AsQueryable();

        if (SelectedTrackFilter is { Id: > 0 })
            employeesQuery = employeesQuery.Where(e => e.TrackId == SelectedTrackFilter.Id);

        if (!string.IsNullOrWhiteSpace(SearchText))
            employeesQuery = employeesQuery.Where(e => EF.Functions.Like(e.Name, $"%{SearchText}%"));

        var employees = employeesQuery.OrderBy(e => e.Track!.Name).ThenBy(e => e.Name).ToList();

        var monthEnd = MonthStart.AddMonths(1).AddDays(-1);
        var entries = db.RosterEntries
            .Where(r => r.Date >= MonthStart && r.Date <= monthEnd)
            .ToList()
            .ToLookup(r => (r.EmployeeId, r.Date));

        Rows.Clear();
        foreach (var emp in employees)
        {
            var row = new EmployeeRowViewModel(emp);
            foreach (var date in Days)
            {
                var entry = entries[(emp.Id, date)].FirstOrDefault();
                var cell = new DayCellViewModel(date, entry?.ShiftCode, ShiftTypes, AssignShift(emp.Id));
                if (holidays.Contains(date) && entry is { ShiftCode: not (null or "OFF" or "LV") })
                {
                    cell.IsFlagged = true;
                    cell.FlagReason = "Falls on a declared holiday";
                }
                row.Days.Add(cell);
            }
            Rows.Add(row);
        }

        StatusText = $"{Rows.Count} employees · {Days.Count} days";
        MonthColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    private Action<DateOnly, string?> AssignShift(string employeeId) => (date, code) =>
    {
        var existing = db.RosterEntries.FirstOrDefault(r => r.EmployeeId == employeeId && r.Date == date);
        if (existing is null)
        {
            if (code is null) return;
            db.RosterEntries.Add(new RosterEntry { EmployeeId = employeeId, Date = date, ShiftCode = code, Source = EntrySource.Manual });
        }
        else if (code is null)
        {
            db.RosterEntries.Remove(existing);
        }
        else
        {
            existing.ShiftCode = code;
            existing.Source = EntrySource.Manual;
        }
        db.SaveChanges();
    };

    [RelayCommand] private void PrevMonth() => MonthStart = MonthStart.AddMonths(-1);
    [RelayCommand] private void NextMonth() => MonthStart = MonthStart.AddMonths(1);

    [RelayCommand]
    private void AddEmployee()
    {
        var window = new Views.EmployeeEditWindow(db, Tracks, null) { Owner = System.Windows.Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            LoadReferenceData();
            LoadMonth();
        }
    }

    [RelayCommand]
    private void EditEmployee(Employee? employee)
    {
        if (employee is null) return;
        var window = new Views.EmployeeEditWindow(db, Tracks, employee) { Owner = System.Windows.Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            LoadReferenceData();
            LoadMonth();
        }
    }

    [RelayCommand]
    private void ManageTracks()
    {
        var window = new Views.TrackManagerWindow(db) { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
        LoadReferenceData();
        LoadMonth();
    }

    [RelayCommand]
    private void ManageShiftTypes()
    {
        var window = new Views.ShiftTypeManagerWindow(db) { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
        LoadReferenceData();
        LoadMonth();
    }

    [RelayCommand]
    private void CopyPreviousMonth()
    {
        var window = new Views.CopyMonthWindow(db, MonthStart) { Owner = System.Windows.Application.Current.MainWindow };
        if (window.ShowDialog() == true)
            LoadMonth();
    }

    [RelayCommand]
    private void ExportImport()
    {
        var window = new Views.ExportImportWindow(db, MonthStart) { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
        LoadReferenceData();
        LoadMonth();
    }
}
