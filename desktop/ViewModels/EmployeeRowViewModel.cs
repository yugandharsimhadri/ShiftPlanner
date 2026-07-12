using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.ViewModels;

public partial class EmployeeRowViewModel : ObservableObject
{
    public Employee Employee { get; }
    public string TrackName { get; }
    public string Name => Employee.Name;
    public string MetaLine => $"{Employee.Id} · {Employee.Subtrack?.Name ?? Employee.Role}";

    public ObservableCollection<DayCellViewModel> Days { get; } = new();

    public EmployeeRowViewModel(Employee employee)
    {
        Employee = employee;
        TrackName = employee.Track?.Name ?? "Unassigned";
    }
}
