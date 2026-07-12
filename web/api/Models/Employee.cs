namespace ShiftPlanner.Api.Models;

public class Employee
{
    // Stable, system-generated, never shown or edited — RosterEntry and other
    // records reference this, so it must never change even if Code does.
    public Guid Id { get; set; } = Guid.NewGuid();

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    // The employer's own reference code (e.g. their payroll/HR system's employee
    // number). Editable, unique per team only — not globally.
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int TrackId { get; set; }
    public Track? Track { get; set; }
    public int? SubtrackId { get; set; }
    public Subtrack? Subtrack { get; set; }
    public string Role { get; set; } = string.Empty;
    public EmploymentType EmploymentType { get; set; }
    public DateOnly JoinDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? Notes { get; set; }
}
