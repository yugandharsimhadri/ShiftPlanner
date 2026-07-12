namespace ShiftPlanner.Desktop.Models;

public class Subtrack
{
    public int Id { get; set; }
    public int TrackId { get; set; }
    public Track? Track { get; set; }
    public string Name { get; set; } = "";

    public List<Employee> Employees { get; set; } = new();
}
