using System.Windows;
using ShiftPlanner.Desktop.Data;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.Views;

public partial class CopyMonthWindow : Window
{
    private readonly ShiftPlannerContext db;
    private readonly DateOnly source;
    private readonly DateOnly target;

    private record PlannedEntry(string EmployeeId, string EmployeeName, DateOnly Date, string ShiftCode, string? FlagReason);

    public CopyMonthWindow(ShiftPlannerContext db, DateOnly targetMonthStart)
    {
        InitializeComponent();
        this.db = db;
        target = targetMonthStart;
        source = targetMonthStart.AddMonths(-1);
        HeaderText.Text = $"Copy {source:MMM yyyy} → {target:MMM yyyy}";
        Pattern_Changed(this, null!);
    }

    private void Pattern_Changed(object sender, RoutedEventArgs e)
    {
        var plan = BuildPlan();
        CountText.Text = plan.Count.ToString();
        var flagged = plan.Where(p => p.FlagReason is not null).ToList();
        FlagCountText.Text = flagged.Count.ToString();
        FlagList.ItemsSource = flagged.Select(f => $"{f.EmployeeName} — {f.Date:MMM d}: {f.FlagReason}").ToList();
    }

    private List<PlannedEntry> BuildPlan()
    {
        var weekdayPattern = WeekdayRadio.IsChecked == true;
        var skipInactive = SkipInactiveCheck.IsChecked == true;

        var holidays = db.Holidays.Select(h => h.Date).ToHashSet();
        var employees = db.Employees.ToList();
        var sourceEntries = db.RosterEntries
            .Where(r => r.Date >= source && r.Date < source.AddMonths(1))
            .ToList()
            .ToLookup(r => r.EmployeeId);

        var plan = new List<PlannedEntry>();

        for (var d = target; d.Month == target.Month; d = d.AddDays(1))
        {
            var mapped = MapDate(d, weekdayPattern);
            if (mapped is null) continue;

            foreach (var emp in employees)
            {
                if (emp.Status == EmployeeStatus.Inactive && skipInactive) continue;

                var entry = sourceEntries[emp.Id].FirstOrDefault(r => r.Date == mapped.Value);
                if (entry?.ShiftCode is null) continue;

                string? flag = null;
                if (emp.Status == EmployeeStatus.Inactive) flag = "employee is marked inactive";
                else if (holidays.Contains(d) && entry.ShiftCode is not ("OFF" or "LV")) flag = "falls on a declared holiday";

                plan.Add(new PlannedEntry(emp.Id, emp.Name, d, entry.ShiftCode, flag));
            }
        }

        return plan;
    }

    private DateOnly? MapDate(DateOnly targetDate, bool weekdayPattern)
    {
        if (!weekdayPattern)
        {
            var candidate = new DateOnly(source.Year, source.Month, 1).AddDays(targetDate.Day - 1);
            return candidate.Month == source.Month ? candidate : null;
        }

        var occurrence = ((targetDate.Day - 1) / 7) + 1;
        var candidates = new List<DateOnly>();
        for (var d = source; d.Month == source.Month; d = d.AddDays(1))
            if (d.DayOfWeek == targetDate.DayOfWeek) candidates.Add(d);

        if (candidates.Count == 0) return null;
        return candidates[Math.Min(occurrence, candidates.Count) - 1];
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var plan = BuildPlan();
        foreach (var p in plan)
        {
            var existing = db.RosterEntries.FirstOrDefault(r => r.EmployeeId == p.EmployeeId && r.Date == p.Date);
            if (existing is null)
                db.RosterEntries.Add(new RosterEntry { EmployeeId = p.EmployeeId, Date = p.Date, ShiftCode = p.ShiftCode, Source = EntrySource.Copied });
            else
            {
                existing.ShiftCode = p.ShiftCode;
                existing.Source = EntrySource.Copied;
            }
        }
        db.SaveChanges();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
