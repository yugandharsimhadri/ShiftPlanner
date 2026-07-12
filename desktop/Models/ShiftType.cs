namespace ShiftPlanner.Desktop.Models;

public class ShiftType
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Color { get; set; } = "#6E7D78";
    public bool IsOvernight { get; set; }
    public int SortOrder { get; set; }
}
