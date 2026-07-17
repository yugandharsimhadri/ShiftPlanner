using ShiftPlanner.Mobile.ViewModels;

namespace ShiftPlanner.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Reflect a login/logout that happened elsewhere since this page was constructed
        // (ShellContent pages are cached and reused between tab visits).
        _viewModel.Refresh();
        _viewModel.LoadCalendarFeedCommand.Execute(null);
    }
}
