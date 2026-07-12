using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace ShiftPlanner.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // The database file lives on a shared network path; another teammate's save can hold
        // the file lock for a moment. Surface that as a retry prompt instead of a crash.
        if (e.Exception is SqliteException { SqliteErrorCode: 5 or 6 } // SQLITE_BUSY / SQLITE_LOCKED
            || (e.Exception.InnerException is SqliteException { SqliteErrorCode: 5 or 6 }))
        {
            MessageBox.Show(
                "Someone else is saving changes right now. Please wait a moment and try again.",
                "Shift Planner", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
            return;
        }

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Shift Planner", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

