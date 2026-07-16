using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;
using Location = ShiftPlanner.Mobile.Models.Location;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>Add or edit a team member. Add mode when "id" isn't passed in the route query;
/// edit mode (with Remove) otherwise. Replaces the old separate Employee add/edit form —
/// this one also carries the AccessRole that used to live on the separate Team tab, since
/// TeamMember now merges both concepts.</summary>
[QueryProperty(nameof(MemberIdText), "id")]
public partial class TeamMemberFormViewModel : ObservableObject
{
    private static readonly string[] RoleOptions = { "Viewer", "Editor", "Admin" };

    private readonly ApiClient _api;
    private int? _memberId;

    [ObservableProperty]
    private string memberIdText = string.Empty;

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
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime joinDate = DateTime.Today;
    [ObservableProperty] private bool isActive = true;

    [ObservableProperty]
    private Track? selectedTrack;

    [ObservableProperty]
    private Subtrack? selectedSubtrack;

    [ObservableProperty]
    private JobRole? selectedJobRole;

    [ObservableProperty]
    private Location? selectedLocation;

    [ObservableProperty]
    private string employmentType = "FullTime";

    [ObservableProperty]
    private string accessRole = "Viewer";

    public ObservableCollection<Track> Tracks { get; } = new();
    public ObservableCollection<Subtrack> Subtracks { get; } = new();
    public ObservableCollection<JobRole> JobRoles { get; } = new();
    public ObservableCollection<Location> Locations { get; } = new();
    public List<string> EmploymentTypes { get; } = new() { "FullTime", "PartTime" };
    public List<string> RoleChoices { get; } = RoleOptions.ToList();

    public TeamMemberFormViewModel(ApiClient api)
    {
        _api = api;
    }

    partial void OnMemberIdTextChanged(string value)
    {
        _memberId = int.TryParse(value, out var id) && id > 0 ? id : null;
        IsEditMode = _memberId.HasValue;
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

            var jobRoles = await _api.GetJobRolesAsync();
            JobRoles.Clear();
            foreach (var role in jobRoles.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                JobRoles.Add(role);
            }

            var locations = await _api.GetLocationsAsync();
            Locations.Clear();
            foreach (var location in locations.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                Locations.Add(location);
            }

            if (_memberId is { } id)
            {
                // No single-member GET on the server — find it in the full list.
                var members = await _api.GetTeamMembersAsync();
                var member = members.FirstOrDefault(m => m.Id == id);
                if (member is null)
                {
                    ErrorMessage = "That team member couldn't be found.";
                    return;
                }

                Code = member.Code;
                Name = member.Name;
                Phone = member.Phone;
                Email = member.Email ?? string.Empty;
                Notes = member.Notes ?? string.Empty;
                JoinDate = member.JoinDate.ToDateTime(TimeOnly.MinValue);
                IsActive = member.Status == "Active";
                EmploymentType = member.EmploymentType;
                AccessRole = member.AccessRole;
                SelectedTrack = Tracks.FirstOrDefault(t => t.Id == member.TrackId);
                SelectedSubtrack = Subtracks.FirstOrDefault(s => s.Id == member.SubtrackId);
                SelectedJobRole = JobRoles.FirstOrDefault(r => r.Id == member.JobRoleId);
                SelectedLocation = Locations.FirstOrDefault(l => l.Id == member.LocationId);
            }
            else if (string.IsNullOrWhiteSpace(Code))
            {
                Code = await _api.GetNextTeamMemberCodeAsync();
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

        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "Code, name, and phone are required.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            if (_memberId is { } id)
            {
                var update = new UpdateTeamMemberRequest
                {
                    Name = Name.Trim(),
                    Phone = Phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Code = Code.Trim(),
                    TrackId = SelectedTrack?.Id,
                    SubtrackId = SelectedSubtrack?.Id,
                    JobRoleId = SelectedJobRole?.Id,
                    LocationId = SelectedLocation?.Id,
                    EmploymentType = EmploymentType,
                    JoinDate = DateOnly.FromDateTime(JoinDate),
                    Status = IsActive ? "Active" : "Inactive",
                    AccessRole = AccessRole,
                };
                await _api.UpdateTeamMemberAsync(id, update);
            }
            else
            {
                var teamId = AppSettingsStore.CurrentTeamId;
                var create = new CreateTeamMemberRequest
                {
                    Name = Name.Trim(),
                    Phone = Phone.Trim(),
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Code = Code.Trim(),
                    TrackId = SelectedTrack?.Id,
                    SubtrackId = SelectedSubtrack?.Id,
                    JobRoleId = SelectedJobRole?.Id,
                    LocationId = SelectedLocation?.Id,
                    EmploymentType = EmploymentType,
                    JoinDate = DateOnly.FromDateTime(JoinDate),
                    Status = IsActive ? "Active" : "Inactive",
                    AccessRole = AccessRole,
                    TeamIds = teamId.HasValue ? new List<int> { teamId.Value } : new List<int>(),
                };
                await _api.CreateTeamMemberAsync(create);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't save the team member. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Shell.Current is null || _memberId is not { } id)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert(
            "Remove team member",
            $"Remove {Name}? Their roster history stays on record but this team member record is removed permanently.",
            "Remove", "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _api.RemoveTeamMemberAsync(id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't remove the team member. {ex.Message}";
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
