namespace ShiftPlanner.Desktop.Models;

public class Employee
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public int TrackId { get; set; }
    public Track? Track { get; set; }
    public int? SubtrackId { get; set; }
    public Subtrack? Subtrack { get; set; }
    public string Role { get; set; } = "";
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public DateOnly JoinDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DayOfWeek? WeeklyOff { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? Notes { get; set; }

    public List<RosterEntry> RosterEntries { get; set; } = new();
}
