using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class LeaveRequestFormPage : ContentPage
{
    public LeaveRequestFormPage(LeaveRequestFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
