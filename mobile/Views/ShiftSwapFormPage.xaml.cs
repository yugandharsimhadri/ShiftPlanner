using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class ShiftSwapFormPage : ContentPage
{
    private readonly ShiftSwapFormViewModel _viewModel;

    public ShiftSwapFormPage(ShiftSwapFormViewModel viewModel)
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
