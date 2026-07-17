using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class ManagerDashboardPage : ContentPage
{
    private readonly ManagerDashboardViewModel _viewModel;

    public ManagerDashboardPage(ManagerDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
