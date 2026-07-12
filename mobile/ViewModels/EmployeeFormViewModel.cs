using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Add or edit an employee. Add mode when "id" isn't passed in the route query;
/// edit mode (with Deactivate/Delete) otherwise.</summary>
[QueryProperty(nameof(EmployeeIdText), "id")]
public partial class EmployeeFormViewModel : ObservableObject
{
    private static readonly string[] WeeklyOffOptions =
        { "None", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    private readonly ApiClient _api;
    private Guid? _employeeId;

    [ObservableProperty]
    private string employeeIdText = string.Empty;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty] private string code = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string phone = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime joinDate = DateTime.Today;
    [ObservableProperty] private bool isActive = true;

    [ObservableProperty]
    private Track? selectedTrack;

    [ObservableProperty]
    private Subtrack? selectedSubtrack;

    [ObservableProperty]
    private string employmentType = "FullTime";

    [ObservableProperty]
    private string weeklyOff = "None";

    public ObservableCollection<Track> Tracks { get; } = new();
    public ObservableCollection<Subtrack> Subtracks { get; } = new();
    public List<string> EmploymentTypes { get; } = new() { "FullTime", "PartTime" };
    public List<string> WeeklyOffChoices { get; } = WeeklyOffOptions.ToList();

    public EmployeeFormViewModel(ApiClient api)
    {
        _api = api;
    }

    partial void OnEmployeeIdTextChanged(string value)
    {
        _employeeId = Guid.TryParse(value, out var id) ? id : null;
        IsEditMode = _employeeId.HasValue;
    }

    partial void OnSelectedTrackChanged(Track? value)
    {
        Subtracks.Clear();
        if (value is null)
        {
            SelectedSubtrack = null;
            return;
        }

        foreach (var sub in value.Subtracks)
        {
            Subtracks.Add(sub);
        }

        if (SelectedSubtrack is not null && SelectedSubtrack.TrackId != value.Id)
        {
            SelectedSubtrack = null;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var tracks = await _api.GetTracksAsync();
            Tracks.Clear();
            foreach (var track in tracks.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                Tracks.Add(track);
            }

            if (_employeeId is { } id)
            {
                var employee = await _api.GetEmployeeAsync(id);
                if (employee is null)
                {
                    ErrorMessage = "That employee couldn't be found.";
                    return;
                }

                Code = employee.Code;
                Name = employee.Name;
                Phone = employee.Phone;
                Email = employee.Email ?? string.Empty;
                Role = employee.Role;
                Notes = employee.Notes ?? string.Empty;
                JoinDate = employee.JoinDate.ToDateTime(TimeOnly.MinValue);
                IsActive = employee.Status == "Active";
                EmploymentType = employee.EmploymentType;
                WeeklyOff = employee.WeeklyOff ?? "None";
                SelectedTrack = Tracks.FirstOrDefault(t => t.Id == employee.TrackId);
                SelectedSubtrack = Subtracks.FirstOrDefault(s => s.Id == employee.SubtrackId);
            }
            else if (string.IsNullOrWhiteSpace(Code))
            {
                Code = await _api.GetNextEmployeeCodeAsync();
                SelectedTrack = Tracks.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load the form. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Phone) || SelectedTrack is null)
        {
            ErrorMessage = "Code, name, phone, and track are required.";
            return;
        }

        var input = new EmployeeInput
        {
            Code = Code.Trim(),
            Name = Name.Trim(),
            Phone = Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            TrackId = SelectedTrack.Id,
            SubtrackId = SelectedSubtrack?.Id,
            Role = Role.Trim(),
            EmploymentType = EmploymentType,
            JoinDate = DateOnly.FromDateTime(JoinDate),
            WeeklyOff = WeeklyOff == "None" ? null : WeeklyOff,
            Status = IsActive ? "Active" : "Inactive",
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        };

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            if (_employeeId is { } id)
            {
                await _api.UpdateEmployeeAsync(id, input);
            }
            else
            {
                await _api.CreateEmployeeAsync(input);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't save the employee. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Shell.Current is null || _employeeId is not { } id)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert(
            "Delete employee",
            $"Delete {Name}? Their roster history stays on record but this employee record is removed permanently.",
            "Delete", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _api.DeleteEmployeeAsync(id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't delete the employee. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
