using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Services;

// Shared by RequireTeamFilter (once a team is already selected) and the
// "list my teams" endpoint (before any team is selected) — both need to
// resolve "does this email have any invite waiting" the same way.
public static class PendingInviteClaimer
{
    public static async Task ClaimAsync(AppDbContext db, string userId, string email)
    {
        var pending = await db.TeamMemberships
            .Where(m => m.UserId == null && m.Email == email)
            .ToListAsync();

        if (pending.Count == 0) return;

        foreach (var invite in pending)
        {
            invite.UserId = userId;
            invite.Status = MembershipStatus.Active;
        }
        await db.SaveChangesAsync();
    }
}
