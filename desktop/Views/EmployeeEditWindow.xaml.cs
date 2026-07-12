using System.Windows;
using System.Windows.Controls;
using ShiftPlanner.Desktop.Data;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.Views;

public partial class EmployeeEditWindow : Window
{
    private readonly ShiftPlannerContext db;
    private readonly Employee employee;
    private readonly bool isNew;

    public EmployeeEditWindow(ShiftPlannerContext db, IEnumerable<Track> tracks, Employee? existing)
    {
        InitializeComponent();
        this.db = db;
        isNew = existing is null;
        employee = existing ?? new Employee { Id = NextEmployeeId(db), JoinDate = DateOnly.FromDateTime(DateTime.Today) };

        HeaderText.Text = isNew ? "New employee" : $"Edit {employee.Name}";
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        TrackCombo.ItemsSource = tracks.ToList();

        IdBox.Text = employee.Id;
        NameBox.Text = employee.Name;
        PhoneBox.Text = employee.Phone;
        EmailBox.Text = employee.Email;
        RoleBox.Text = employee.Role;
        EmploymentTypeCombo.SelectedIndex = employee.EmploymentType == EmploymentType.PartTime ? 1 : 0;
        JoinDatePicker.SelectedDate = employee.JoinDate.ToDateTime(TimeOnly.MinValue);
        WeeklyOffCombo.SelectedIndex = employee.WeeklyOff is null ? 0 : (int)employee.WeeklyOff.Value + 1;
        StatusCombo.SelectedIndex = employee.Status == EmployeeStatus.Inactive ? 1 : 0;
        NotesBox.Text = employee.Notes;

        var track = tracks.FirstOrDefault(t => t.Id == employee.TrackId) ?? tracks.FirstOrDefault();
        TrackCombo.SelectedItem = track;
        if (employee.SubtrackId is int subId)
            SubtrackCombo.SelectedItem = ((List<Subtrack>)SubtrackCombo.ItemsSource).FirstOrDefault(s => s.Id == subId);
    }

    private static string NextEmployeeId(ShiftPlannerContext db)
    {
        var max = db.Employees.ToList()
            .Select(e => int.TryParse(e.Id.Replace("EMP-", ""), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"EMP-{max + 1:000}";
    }

    private void TrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var track = TrackCombo.SelectedItem as Track;
        SubtrackCombo.ItemsSource = track is null ? null : db.Subtracks.Where(s => s.TrackId == track.Id).ToList();
        SubtrackCombo.SelectedIndex = -1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(PhoneBox.Text) || TrackCombo.SelectedItem is null)
        {
            ErrorText.Text = "Name, phone, and track are required.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        employee.Name = NameBox.Text.Trim();
        employee.Phone = PhoneBox.Text.Trim();
        employee.Email = string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim();
        employee.Role = RoleBox.Text.Trim();
        employee.TrackId = ((Track)TrackCombo.SelectedItem).Id;
        employee.SubtrackId = (SubtrackCombo.SelectedItem as Subtrack)?.Id;
        employee.EmploymentType = EmploymentTypeCombo.SelectedIndex == 1 ? EmploymentType.PartTime : EmploymentType.FullTime;
        employee.JoinDate = DateOnly.FromDateTime(JoinDatePicker.SelectedDate ?? DateTime.Today);
        employee.WeeklyOff = WeeklyOffCombo.SelectedIndex <= 0 ? null : (DayOfWeek)(WeeklyOffCombo.SelectedIndex - 1);
        employee.Status = StatusCombo.SelectedIndex == 1 ? EmployeeStatus.Inactive : EmployeeStatus.Active;
        employee.Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();

        if (isNew) db.Employees.Add(employee);
        db.SaveChanges();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var shiftCount = db.RosterEntries.Count(r => r.EmployeeId == employee.Id);
        var message = shiftCount > 0
            ? $"Delete {employee.Name}? This also removes {shiftCount} scheduled shift entr{(shiftCount == 1 ? "y" : "ies")} for them. This can't be undone.\n\nIf you just want to stop scheduling them but keep their history, set Status to Inactive instead."
            : $"Delete {employee.Name}? This can't be undone.";

        var result = MessageBox.Show(message, "Delete employee", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        db.Employees.Remove(employee);
        db.SaveChanges();

        DialogResult = true;
        Close();
    }
}
