using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;

namespace ShiftPlanner.Api.Services;

// Shared by RequireTeamFilter (once a team is already selected) and the
// "list my teams" endpoint (before any team is selected) — both need to
// resolve "does this login match any not-yet-claimed Person" the same way.
// Matches by email OR phone number, since login is optional and can happen
// either way (see Person.cs).
public static class PendingInviteClaimer
{
    public static async Task ClaimAsync(AppDbContext db, string userId, string? email, string? phone)
    {
        var pending = await db.People
            .Where(p => p.UserId == null &&
                ((email != null && p.Email == email) || (phone != null && p.Phone == phone)))
            .ToListAsync();

        if (pending.Count == 0) return;

        foreach (var person in pending)
        {
            person.UserId = userId;
        }
        await db.SaveChangesAsync();
    }
}
