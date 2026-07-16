using Microsoft.Extensions.Logging;
using ShiftPlanner.Mobile.Services;
using ShiftPlanner.Mobile.ViewModels;
using ShiftPlanner.Mobile.Views;

namespace ShiftPlanner.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Networking: one shared HttpClient wrapped by the typed ApiClient. Base address
        // and bearer token are attached per-request (see ApiClient) since both can change
        // at runtime (server address is user-editable, token is set/cleared by login/logout).
        builder.Services.AddSingleton(_ => new HttpClient());
        builder.Services.AddSingleton<ApiClient>();

        // Pages + view models. Transient so Shell can construct each ShellContent's page
        // (and its view model) via constructor injection.
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<TeamGatePage>();
        builder.Services.AddTransient<TeamGateViewModel>();
        builder.Services.AddTransient<RosterPage>();
        builder.Services.AddTransient<RosterViewModel>();
        builder.Services.AddTransient<TeamMembersPage>();
        builder.Services.AddTransient<TeamMembersViewModel>();
        builder.Services.AddTransient<TeamMemberFormPage>();
        builder.Services.AddTransient<TeamMemberFormViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ProfileViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
