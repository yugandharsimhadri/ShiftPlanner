using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

public class RosterEntry
{
    public int Id { get; set; }
    public int TeamId { get; set; }

    public Guid EmployeeId { get; set; }

    [JsonIgnore]
    public Employee? Employee { get; set; }

    public DateOnly Date { get; set; }

    // References ShiftType.Code within the same team. Not modeled as a formal EF
    // relationship (ShiftType.Code is only unique per-team, not globally, and a
    // composite FK here isn't worth the complexity) — validated at the API layer
    // on write instead.
    public string? ShiftCode { get; set; }

    public RosterEntrySource Source { get; set; } = RosterEntrySource.Manual;
    public string? Note { get; set; }
}
