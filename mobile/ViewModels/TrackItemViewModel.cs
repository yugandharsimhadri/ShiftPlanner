using ShiftPlanner.Mobile.Models;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One track row on the Settings tab, with its subtracks flattened for display.</summary>
public sealed class TrackItemViewModel
{
    public required Track Track { get; init; }
    public int Id => Track.Id;
    public string Name => Track.Name;
    public string LeadLabel => string.IsNullOrWhiteSpace(Track.LeadName) ? "No lead set" : $"Lead: {Track.LeadName}";
    public Color SwatchColor => Color.TryParse(Track.Color, out var c) ? c : Colors.Gray;

    public string SubtracksLabel => Track.Subtracks.Count == 0
        ? "No subtracks"
        : string.Join(", ", Track.Subtracks.Select(s => s.Name));
}

/// <summary>One shift-type row on the Settings tab.</summary>
public sealed class ShiftTypeItemViewModel
{
    public required ShiftTypeFull ShiftType { get; init; }
    public int Id => ShiftType.Id;
    public string Code => ShiftType.Code;
    public string Name => ShiftType.Name;
    public Color SwatchColor => Color.TryParse(ShiftType.Color, out var c) ? c : Colors.Gray;

    public string TimeRangeLabel
    {
        get
        {
            if (ShiftType.Start is null || ShiftType.End is null)
            {
                return ShiftType.IsOvernight ? "Overnight" : "No fixed hours";
            }

            var range = $"{ShiftType.Start:h:mm tt} – {ShiftType.End:h:mm tt}";
            return ShiftType.IsOvernight ? $"{range} (overnight)" : range;
        }
    }
}
