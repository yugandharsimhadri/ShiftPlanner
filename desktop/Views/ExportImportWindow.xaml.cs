using System.IO;
using System.Text;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using ShiftPlanner.Desktop.Data;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.Views;

public partial class ExportImportWindow : Window
{
    private readonly ShiftPlannerContext db;
    private readonly DateOnly monthStart;
    private List<Employee>? pendingValidRows;

    public ExportImportWindow(ShiftPlannerContext db, DateOnly monthStart)
    {
        InitializeComponent();
        this.db = db;
        this.monthStart = monthStart;
        HeaderText.Text = $"Import & export — {monthStart:MMM yyyy}";
    }

    // ---- Export ----

    private (List<Employee> employees, List<DateOnly> days, ILookup<(string, DateOnly), RosterEntry> entries) GatherMonthData()
    {
        var employees = db.Employees
            .Include(e => e.Track)
            .Include(e => e.Subtrack)
            .Where(e => e.Status == EmployeeStatus.Active)
            .OrderBy(e => e.Name)
            .ToList();
        var days = new List<DateOnly>();
        for (var d = monthStart; d.Month == monthStart.Month; d = d.AddDays(1)) days.Add(d);
        var lastDay = days[days.Count - 1];
        var entries = db.RosterEntries.Where(r => r.Date >= monthStart && r.Date <= lastDay).ToList().ToLookup(r => (r.EmployeeId, r.Date));
        return (employees, days, entries);
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = $"shiftplanner-{monthStart:yyyy-MM}.xlsx", Filter = "Excel Workbook (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog() != true) return;

        var (employees, days, entries) = GatherMonthData();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(monthStart.ToString("MMM yyyy"));

        sheet.Cell(1, 1).Value = "Employee";
        sheet.Cell(1, 2).Value = "ID";
        sheet.Cell(1, 3).Value = "Track";
        sheet.Cell(1, 4).Value = "Subtrack";
        for (var i = 0; i < days.Count; i++)
            sheet.Cell(1, 5 + i).Value = days[i].ToString("MMM d");

        var row = 2;
        foreach (var emp in employees)
        {
            sheet.Cell(row, 1).Value = emp.Name;
            sheet.Cell(row, 2).Value = emp.Id;
            sheet.Cell(row, 3).Value = emp.Track?.Name ?? "";
            sheet.Cell(row, 4).Value = emp.Subtrack?.Name ?? "";
            for (var i = 0; i < days.Count; i++)
                sheet.Cell(row, 5 + i).Value = entries[(emp.Id, days[i])].FirstOrDefault()?.ShiftCode ?? "";
            row++;
        }
        sheet.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        ExportStatusText.Text = $"Saved {dialog.FileName}";
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = $"shiftplanner-{monthStart:yyyy-MM}.csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog() != true) return;

        var (employees, days, entries) = GatherMonthData();
        var sb = new StringBuilder();
        sb.Append("Employee,ID,Track,Subtrack,").AppendLine(string.Join(",", days.Select(d => d.ToString("MMM d"))));
        foreach (var emp in employees)
        {
            sb.Append(CsvField(emp.Name)).Append(',').Append(emp.Id).Append(',')
              .Append(CsvField(emp.Track?.Name ?? "")).Append(',').Append(CsvField(emp.Subtrack?.Name ?? "")).Append(',');
            sb.AppendLine(string.Join(",", days.Select(d => entries[(emp.Id, d)].FirstOrDefault()?.ShiftCode ?? "")));
        }
        File.WriteAllText(dialog.FileName, sb.ToString());
        ExportStatusText.Text = $"Saved {dialog.FileName}";
    }

    private static string CsvField(string value) => value.Contains(',') ? $"\"{value}\"" : value;

    // ---- Import ----

    private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = "shiftplanner-employee-template.csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog() != true) return;
        var sample = "Name,Phone,Email,Track,Subtrack,Role,EmploymentType,JoinDate,WeeklyOff,Notes\n" +
                     "Priya Nair,98450 12233,,Warehouse,Outbound,Picker,FullTime,2026-07-01,Sunday,\n";
        File.WriteAllText(dialog.FileName, sample);
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog() != true) return;

        var lines = File.ReadAllLines(dialog.FileName).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count < 2)
        {
            ValidationList.ItemsSource = new[] { "File has no data rows." };
            return;
        }

        var header = ParseCsvLine(lines[0]).Select(h => h.Trim()).ToList();
        var tracks = db.Tracks.ToList();
        var results = new List<string>();
        var valid = new List<Employee>();
        var maxId = db.Employees.ToList().Select(emp => int.TryParse(emp.Id.Replace("EMP-", ""), out var n) ? n : 0).DefaultIfEmpty(0).Max();

        for (var i = 1; i < lines.Count; i++)
        {
            var cells = ParseCsvLine(lines[i]);
            string Col(string name)
            {
                var idx = header.IndexOf(name);
                return idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";
            }

            var name = Col("Name");
            var phone = Col("Phone");
            var trackName = Col("Track");
            var track = tracks.FirstOrDefault(t => string.Equals(t.Name, trackName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(name)) { results.Add($"Row {i + 1} — name is required, skipped"); continue; }
            if (string.IsNullOrWhiteSpace(phone)) { results.Add($"Row {i + 1} — phone is required, skipped"); continue; }
            if (track is null) { results.Add($"Row {i + 1} — track \"{trackName}\" not found, skipped"); continue; }

            var subtrackName = Col("Subtrack");
            var subtrack = db.Subtracks.FirstOrDefault(s => s.TrackId == track.Id && string.Equals(s.Name, subtrackName, StringComparison.OrdinalIgnoreCase));

            maxId++;
            var employmentType = string.Equals(Col("EmploymentType"), "PartTime", StringComparison.OrdinalIgnoreCase) ? EmploymentType.PartTime : EmploymentType.FullTime;
            DayOfWeek? weeklyOff = Enum.TryParse<DayOfWeek>(Col("WeeklyOff"), true, out var wd) ? wd : null;
            var joinDate = DateOnly.TryParse(Col("JoinDate"), out var jd) ? jd : DateOnly.FromDateTime(DateTime.Today);

            valid.Add(new Employee
            {
                Id = $"EMP-{maxId:000}", Name = name, Phone = phone,
                Email = string.IsNullOrWhiteSpace(Col("Email")) ? null : Col("Email"),
                TrackId = track.Id, SubtrackId = subtrack?.Id, Role = Col("Role"),
                EmploymentType = employmentType, JoinDate = joinDate, WeeklyOff = weeklyOff,
                Notes = string.IsNullOrWhiteSpace(Col("Notes")) ? null : Col("Notes")
            });
            results.Add($"Row {i + 1} — {name} ✓ ready to import");
        }

        pendingValidRows = valid;
        ValidationList.ItemsSource = results;
        ImportButton.IsEnabled = valid.Count > 0;
        ImportButton.Content = $"Import {valid.Count} valid row(s)";
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (pendingValidRows is null || pendingValidRows.Count == 0) return;
        db.Employees.AddRange(pendingValidRows);
        db.SaveChanges();
        MessageBox.Show($"Imported {pendingValidRows.Count} employee(s).", "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
        pendingValidRows = null;
        ImportButton.IsEnabled = false;
        ImportButton.Content = "Import valid rows";
        ValidationList.ItemsSource = null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}
