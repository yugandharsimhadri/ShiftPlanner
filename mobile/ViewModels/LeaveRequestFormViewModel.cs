using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Mobile.Services;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>POST /api/leave-requests — request time off for oneself. Most teams
/// auto-approve on creation (Team.AutoApproveLeaveRequests), so this often resolves
/// immediately rather than sitting Pending.</summary>
public partial class LeaveRequestFormViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today;

    [ObservableProperty]
    private string reason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool isSaving;

    public bool CanSubmit => !IsSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public LeaveRequestFormViewModel(ApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (EndDate < StartDate)
        {
            ErrorMessage = "End date can't be before the start date.";
            return;
        }

        try
        {
            IsSaving = true;
            ErrorMessage = null;
            await _api.CreateLeaveRequestAsync(
                DateOnly.FromDateTime(StartDate),
                DateOnly.FromDateTime(EndDate),
                string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim());
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is ApiException apiEx ? apiEx.Message : $"Couldn't request leave. {ex.Message}";
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
