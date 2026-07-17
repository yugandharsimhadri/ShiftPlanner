using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Models;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>GET /api/manager/availability — live availability across every team the
/// signed-in person manages (read-only oversight, not roster-edit rights). Reached from
/// the Profile tab's "View teams you manage" link; only meaningful if the caller has been
/// granted manager access to at least one team beyond their current one.</summary>
public partial class ManagerDashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    private bool hasNoManagedTeams;

    public ObservableCollection<ManagerTeamAvailability> Teams { get; } = new();

    public ManagerDashboardViewModel(ApiClient api)
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

            var teams = await _api.GetManagerAvailabilityAsync();
            Teams.Clear();
            foreach (var team in teams)
            {
                Teams.Add(team);
            }
            HasNoManagedTeams = Teams.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't load managed teams. {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
