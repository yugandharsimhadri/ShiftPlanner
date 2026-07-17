using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class RosterHistoryPage : ContentPage
{
    private readonly RosterHistoryViewModel _viewModel;

    public RosterHistoryPage(RosterHistoryViewModel viewModel)
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
