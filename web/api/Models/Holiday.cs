namespace ShiftPlanner.Api.Models;

public class Holiday
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}
