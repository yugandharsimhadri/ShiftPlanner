using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class LivePage : ContentPage
{
    private readonly LiveViewModel _viewModel;

    public LivePage(LiveViewModel viewModel)
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
