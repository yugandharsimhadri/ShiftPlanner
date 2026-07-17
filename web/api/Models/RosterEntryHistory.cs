using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

// A lightweight append-only change log for roster entries — not full undo/redo, just
// enough to answer "who changed this shift, and what did it used to be" after the fact.
// Written by RosterEndpoints.UpsertEntryAsync, the single shared path every roster write
// (manual, bulk, pattern, copy-forward, swap-approval) goes through.
public class RosterEntryHistory
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int TeamMemberId { get; set; }

    [JsonIgnore]
    public TeamMember? TeamMember { get; set; }

    public DateOnly Date { get; set; }

    public string? OldShiftCode { get; set; }
    public string? NewShiftCode { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    // What triggered the change — free-form label rather than a shared enum with
    // RosterEntrySource, since this needs a couple of extra cases (bulk/pattern/swap)
    // that aren't meaningful on RosterEntry itself.
    public string Source { get; set; } = "Manual";
}
