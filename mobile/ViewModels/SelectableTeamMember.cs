using CommunityToolkit.Mvvm.ComponentModel;
using ShiftPlanner.Mobile.Models;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>A team member plus a checkbox state — used by Bulk Assign and Apply Pattern's
/// member-picker lists, neither of which needs this on <see cref="TeamMember"/> itself.</summary>
public sealed partial class SelectableTeamMember : ObservableObject
{
    public required TeamMember Member { get; init; }

    [ObservableProperty]
    private bool isSelected;

    public string Name => Member.Name;
    public string Code => Member.Code;
}
