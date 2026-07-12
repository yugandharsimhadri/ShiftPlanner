using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

public partial class EmployeesViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private List<EmployeeListItemViewModel> _all = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ObservableCollection<EmployeeListItemViewModel> Employees { get; } = new();

    public EmployeesViewModel(ApiClient api)
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

            var employees = await _api.GetEmployeesAsync();
            _all = employees
                .Select(e => new EmployeeListItemViewModel
                {
                    Id = e.Id,
                    Code = e.Code,
                    Name = e.Name,
                    TrackName = e.Track?.Name ?? "",
                    Status = e.Status,
                })
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load employees. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Employees.Clear();

        IEnumerable<EmployeeListItemViewModel> source = _all;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            source = source.Where(e =>
                e.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.TrackName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var employee in source)
        {
            Employees.Add(employee);
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("employeeform");
        }
    }

    [RelayCommand]
    private async Task OpenAsync(EmployeeListItemViewModel item)
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (CanEdit)
        {
            await Shell.Current.GoToAsync($"employeeform?id={item.Id}");
        }
        else
        {
            await Shell.Current.DisplayAlert(item.Name,
                $"Code: {item.Code}\nTrack: {item.TrackName}\nStatus: {item.Status}", "OK");
        }
    }
}
