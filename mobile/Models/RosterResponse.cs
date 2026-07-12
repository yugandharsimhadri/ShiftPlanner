namespace ShiftPlanner.Mobile.Models;

/// <summary>
/// Raw shape of GET /api/roster?year=&amp;month= — the same payload the Web app's grid
/// consumes. Mobile has no per-employee filter on the server (there's no link yet between
/// a login and a roster row), so it fetches the whole team's month and derives "my shifts"
/// / "today" client-side from this.
/// </summary>
public sealed class RosterResponse
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<RosterEntryDto> Entries { get; set; } = new();
    public List<EmployeeSummaryDto> Employees { get; set; } = new();
    public List<ShiftTypeDto> ShiftTypes { get; set; } = new();
}

public sealed class RosterEntryDto
{
    public Guid EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public string? ShiftCode { get; set; }
}

public sealed class EmployeeSummaryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TrackRefDto? Track { get; set; }
}

public sealed class TrackRefDto
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ShiftTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public bool IsOvernight { get; set; }
}
