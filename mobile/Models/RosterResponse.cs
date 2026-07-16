namespace ShiftPlanner.Mobile.Models;

/// <summary>
/// Raw shape of GET /api/roster?year=&amp;month= — the same payload the Web app's grid
/// consumes. The server returns raw TeamMember/RosterEntry/ShiftType entities (not DTOs),
/// so TeamMembers carry a nested Person and Track object rather than flat fields. Mobile has
/// no per-employee filter on the server, so it fetches the whole team's month and derives
/// "my shifts" / "today" client-side from this.
/// </summary>
public sealed class RosterResponse
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<RosterEntryDto> Entries { get; set; } = new();
    public List<RosterTeamMemberDto> TeamMembers { get; set; } = new();
    public List<ShiftTypeDto> ShiftTypes { get; set; } = new();
}

public sealed class RosterEntryDto
{
    public int TeamMemberId { get; set; }
    public DateOnly Date { get; set; }
    public string? ShiftCode { get; set; }
}

/// <summary>Just the fields the Roster day view actually reads — the real payload carries
/// the full TeamMember entity (Person, Track, JobRole, Location, etc.), everything else is
/// ignored on deserialize.</summary>
public sealed class RosterTeamMemberDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public PersonRefDto? Person { get; set; }
    public TrackRefDto? Track { get; set; }
}

public sealed class PersonRefDto
{
    public string Name { get; set; } = string.Empty;
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
