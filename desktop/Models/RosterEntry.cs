namespace ShiftPlanner.Desktop.Models;

public class RosterEntry
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = "";
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    public string? ShiftCode { get; set; }
    public ShiftType? ShiftTypeRef { get; set; }
    public EntrySource Source { get; set; } = EntrySource.Manual;
    public string? Note { get; set; }
}
