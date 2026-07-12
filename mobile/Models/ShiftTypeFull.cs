namespace ShiftPlanner.Mobile.Models;

/// <summary>Full shift-type record for the Settings tab's CRUD screen. The roster payload
/// (RosterResponse.ShiftTypeDto) carries only what the day view needs to render a chip.</summary>
public sealed class ShiftTypeFull
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Color { get; set; } = "#2F7D6B";
    public bool IsOvernight { get; set; }
}

/// <summary>Body for POST/PUT /api/shift-types.</summary>
public sealed class ShiftTypeInput
{
    public int? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Color { get; set; } = "#2F7D6B";
    public bool IsOvernight { get; set; }
}
