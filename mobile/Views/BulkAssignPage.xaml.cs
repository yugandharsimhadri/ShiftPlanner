using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class BulkAssignPage : ContentPage
{
    private readonly BulkAssignViewModel _viewModel;

    public BulkAssignPage(BulkAssignViewModel viewModel)
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
