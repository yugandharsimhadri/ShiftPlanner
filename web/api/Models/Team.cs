using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftPlanner.Api.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;

    // --- Team settings ---------------------------------------------------
    // All optional/informational — a team works fine with none of these set.

    public string? OrgName { get; set; }

    // A budgeted headcount the lead can set for reference — shown next to the
    // actual employee count, never enforced (a team can go over or under it).
    public int? TeamStrength { get; set; }

    // Free-text description of coverage, e.g. "24x7" or "Day shift only".
    public string? ShiftsCovered { get; set; }

    // Comma-separated DayOfWeek names, e.g. "Saturday,Sunday" — the days the
    // roster treats as everyone's default weekly off unless a real shift is
    // assigned. Defaults to the weekend; a team can reconfigure or clear it.
    public string DefaultOffDaysCsv { get; set; } = "Saturday,Sunday";

    // Whether working a default-off day auto-earns a comp-off (see CompOffEntry).
    // Off by default in code (DbSeeder turns it on for the demo team) so a team
    // that doesn't want the feature never sees it appear.
    public bool CompOffsEnabled { get; set; }

    [NotMapped]
    public List<DayOfWeek> DefaultOffDays
    {
        get => DefaultOffDaysCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => Enum.TryParse<DayOfWeek>(d, true, out var day) ? day : (DayOfWeek?)null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();
        set => DefaultOffDaysCsv = string.Join(",", value.Distinct());
    }
}
