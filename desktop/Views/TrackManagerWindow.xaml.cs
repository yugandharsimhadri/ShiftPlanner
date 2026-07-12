using System.Windows;
using ShiftPlanner.Desktop.Data;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.Views;

public partial class TrackManagerWindow : Window
{
    private readonly ShiftPlannerContext db;

    public TrackManagerWindow(ShiftPlannerContext db)
    {
        InitializeComponent();
        this.db = db;
        RefreshTracks();
    }

    private void RefreshTracks()
    {
        var selectedId = (TracksList.SelectedItem as Track)?.Id;
        TracksList.ItemsSource = db.Tracks.OrderBy(t => t.Name).ToList();
        if (selectedId is int id)
            TracksList.SelectedItem = ((List<Track>)TracksList.ItemsSource).FirstOrDefault(t => t.Id == id);
        else if (TracksList.Items.Count > 0)
            TracksList.SelectedIndex = 0;
    }

    private void TracksList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var track = TracksList.SelectedItem as Track;
        SubtracksList.ItemsSource = track is null ? null : db.Subtracks.Where(s => s.TrackId == track.Id).OrderBy(s => s.Name).ToList();
    }

    private static readonly string[] Palette = { "#4453AD", "#A8701F", "#0F6E67", "#22314F", "#A5392B", "#6E7D78" };

    private void AddTrack_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewTrackBox.Text)) return;
        var color = Palette[db.Tracks.Count() % Palette.Length];
        db.Tracks.Add(new Track { Name = NewTrackBox.Text.Trim(), Color = color });
        db.SaveChanges();
        NewTrackBox.Clear();
        RefreshTracks();
    }

    private void DeleteTrack_Click(object sender, RoutedEventArgs e)
    {
        if (TracksList.SelectedItem is not Track track) return;
        if (db.Employees.Any(emp => emp.TrackId == track.Id))
        {
            MessageBox.Show("This track still has employees assigned. Reassign them before deleting.", "Can't delete", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        db.Tracks.Remove(track);
        db.SaveChanges();
        RefreshTracks();
    }

    private void AddSubtrack_Click(object sender, RoutedEventArgs e)
    {
        if (TracksList.SelectedItem is not Track track || string.IsNullOrWhiteSpace(NewSubtrackBox.Text)) return;
        db.Subtracks.Add(new Subtrack { TrackId = track.Id, Name = NewSubtrackBox.Text.Trim() });
        db.SaveChanges();
        NewSubtrackBox.Clear();
        TracksList_SelectionChanged(this, null!);
    }

    private void DeleteSubtrack_Click(object sender, RoutedEventArgs e)
    {
        if (SubtracksList.SelectedItem is not Subtrack sub) return;
        db.Subtracks.Remove(sub);
        db.SaveChanges();
        TracksList_SelectionChanged(this, null!);
    }
}
