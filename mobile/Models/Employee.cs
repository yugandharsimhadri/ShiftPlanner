namespace ShiftPlanner.Mobile.Models;

/// <summary>Full employee record, as returned by GET /api/employees and friends.</summary>
public sealed class Employee
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int TrackId { get; set; }
    public Track? Track { get; set; }
    public int? SubtrackId { get; set; }
    public Subtrack? Subtrack { get; set; }
    public string Role { get; set; } = string.Empty;

    /// <summary>"FullTime" or "PartTime".</summary>
    public string EmploymentType { get; set; } = "FullTime";
    public DateOnly JoinDate { get; set; }

    /// <summary>Day-of-week name, e.g. "Sunday", or null.</summary>
    public string? WeeklyOff { get; set; }

    /// <summary>"Active" or "Inactive".</summary>
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
}

/// <summary>Body for POST/PUT /api/employees — mirrors the server's EmployeeDto.</summary>
public sealed class EmployeeInput
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int TrackId { get; set; }
    public int? SubtrackId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = "FullTime";
    public DateOnly JoinDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? WeeklyOff { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
}
