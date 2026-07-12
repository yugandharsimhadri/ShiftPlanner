using ShiftPlanner.Mobile.Models;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One row on the Team tab.</summary>
public sealed class MemberItemViewModel
{
    public required Membership Membership { get; init; }
    public int Id => Membership.Id;
    public string Email => Membership.Email;
    public string Role => Membership.Role;
    public string StatusLabel => Membership.Status == "Invited" ? "Invited — awaiting first sign-in" : "Active";
    public string LinkedLabel { get; init; } = "Not linked";
}
