using System.Collections.ObjectModel;
using System.Windows;
using ShiftPlanner.Desktop.Data;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.Views;

public partial class ShiftTypeManagerWindow : Window
{
    private readonly ShiftPlannerContext db;
    private readonly ObservableCollection<EditableShiftType> rows = new();

    public ShiftTypeManagerWindow(ShiftPlannerContext db)
    {
        InitializeComponent();
        this.db = db;

        foreach (var s in db.ShiftTypes.OrderBy(s => s.SortOrder))
            rows.Add(new EditableShiftType
            {
                Code = s.Code,
                Name = s.Name,
                StartText = s.Start?.ToString("HH:mm") ?? "",
                EndText = s.End?.ToString("HH:mm") ?? "",
                Color = s.Color,
                IsOvernight = s.IsOvernight
            });

        Grid.ItemsSource = rows;
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
        => rows.Add(new EditableShiftType { Code = "NEW", Name = "New shift", Color = "#6E7D78" });

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is EditableShiftType row) rows.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit();
        Grid.CommitEdit();

        var existingCodes = db.ShiftTypes.Select(s => s.Code).ToList();
        var newCodes = rows.Select(r => r.Code).ToHashSet();

        foreach (var code in existingCodes.Where(c => !newCodes.Contains(c)))
            db.ShiftTypes.Remove(db.ShiftTypes.First(s => s.Code == code));

        var order = 1;
        foreach (var row in rows)
        {
            TimeOnly.TryParse(row.StartText, out var start);
            TimeOnly.TryParse(row.EndText, out var end);
            var existing = db.ShiftTypes.FirstOrDefault(s => s.Code == row.Code);
            if (existing is null)
            {
                db.ShiftTypes.Add(new ShiftType
                {
                    Code = row.Code, Name = row.Name,
                    Start = string.IsNullOrWhiteSpace(row.StartText) ? null : start,
                    End = string.IsNullOrWhiteSpace(row.EndText) ? null : end,
                    Color = row.Color, IsOvernight = row.IsOvernight, SortOrder = order
                });
            }
            else
            {
                existing.Name = row.Name;
                existing.Start = string.IsNullOrWhiteSpace(row.StartText) ? null : start;
                existing.End = string.IsNullOrWhiteSpace(row.EndText) ? null : end;
                existing.Color = row.Color;
                existing.IsOvernight = row.IsOvernight;
                existing.SortOrder = order;
            }
            order++;
        }

        db.SaveChanges();
        Close();
    }
}

public class EditableShiftType
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string StartText { get; set; } = "";
    public string EndText { get; set; } = "";
    public string Color { get; set; } = "#6E7D78";
    public bool IsOvernight { get; set; }
}
