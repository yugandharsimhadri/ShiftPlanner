namespace ShiftPlanner.Api.Models;

// A human being, independent of any team. Login is optional — most of the fields below
// exist whether or not this person ever signs in. If they do sign in (via email or phone
// number, both checked against IdentityUser), UserId gets claimed the same way a pending
// invite always has (see PendingInviteClaimer) — nothing about the login flow requires a
// Person to have signed up first.
public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }

    // Null until this person signs in with a matching email or phone number.
    public string? UserId { get; set; }

    // Whoever added this person — scopes "assign this same person to one of my other
    // teams" to people the acting admin already manages somewhere, not a global directory.
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
