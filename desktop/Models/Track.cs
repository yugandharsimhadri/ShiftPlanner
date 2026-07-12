namespace ShiftPlanner.Desktop.Models;

public class Track
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? LeadName { get; set; }
    public string Color { get; set; } = "#4453AD";

    public List<Subtrack> Subtracks { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
}
