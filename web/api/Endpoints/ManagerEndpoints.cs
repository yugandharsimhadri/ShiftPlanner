using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

// A Manager oversees the live-availability dashboard across several teams without
// gaining roster-edit rights on any of them — see ManagerAssignment. Granting/revoking
// is per-team (an Admin manages oversight of their own team only); the cross-team
// dashboard itself is not team-scoped, since its whole point is to ignore X-Team-Id.
public static class ManagerEndpoints
{
    public static void MapManagerEndpoints(this WebApplication app)
    {
        var teamGroup = app.MapGroup("/api/teams/current/managers").RequireAuthorization();

        teamGroup.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var managers = await db.ManagerAssignments
                .Where(a => a.TeamId == teamId)
                .Include(a => a.Person)
                .Include(a => a.Team)
                .OrderBy(a => a.Person!.Name)
                .Select(a => new ManagerAssignmentDto(a.Id, a.PersonId, a.Person!.Name, a.Person.Phone ?? "", a.TeamId, a.Team!.Name))
                .ToListAsync();
            return Results.Ok(managers);
        }).RequireTeamAdmin();

        // People eligible to become a manager of this team: anyone the acting admin
        // already knows (added anywhere) or who's already on this team — never an
        // arbitrary cross-tenant search.
        teamGroup.MapGet("/search", async (string? phone, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            if (string.IsNullOrWhiteSpace(phone)) return Results.Ok(new List<PersonSearchResultDto>());

            var query = phone.Trim();
            var candidates = await db.People
                .Where(p =>
                    p.Phone != null && p.Phone.Contains(query) &&
                    (p.CreatedByUserId == ctx.UserId || db.TeamMembers.Any(m => m.TeamId == ctx.TeamId && m.PersonId == p.Id)))
                .Select(p => new PersonSearchResultDto(p.Id, p.Name, p.Phone ?? "", p.Email))
                .Take(10)
                .ToListAsync();

            return Results.Ok(candidates);
        }).RequireTeamAdmin();

        teamGroup.MapPost("", async (GrantManagerDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var person = await db.People.FirstOrDefaultAsync(p =>
                p.Id == dto.PersonId &&
                (p.CreatedByUserId == ctx.UserId || db.TeamMembers.Any(m => m.TeamId == ctx.TeamId && m.PersonId == p.Id)));
            if (person is null) return Results.NotFound();

            var alreadyGranted = await db.ManagerAssignments.AnyAsync(a => a.PersonId == person.Id && a.TeamId == ctx.TeamId);
            if (alreadyGranted)
                return Results.Conflict(new { message = $"{person.Name} already manages this team." });

            var assignment = new ManagerAssignment { PersonId = person.Id, TeamId = ctx.TeamId, GrantedByUserId = ctx.UserId };
            db.ManagerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            var team = await db.Teams.FirstAsync(t => t.Id == ctx.TeamId);
            return Results.Created($"/api/teams/current/managers/{assignment.Id}",
                new ManagerAssignmentDto(assignment.Id, person.Id, person.Name, person.Phone ?? "", ctx.TeamId, team.Name));
        }).RequireTeamAdmin();

        teamGroup.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var assignment = await db.ManagerAssignments.FirstOrDefaultAsync(a => a.Id == id && a.TeamId == teamId);
            if (assignment is null) return Results.NotFound();

            db.ManagerAssignments.Remove(assignment);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamAdmin();

        // --- Cross-team dashboard (deliberately not team-scoped) -----------------

        var managerGroup = app.MapGroup("/api/manager").RequireAuthorization();

        managerGroup.MapGet("/teams", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await ClaimPendingAsync(db, userId);

            var teams = await db.ManagerAssignments
                .Where(a => a.Person!.UserId == userId)
                .Select(a => new ManagerTeamDto(a.TeamId, a.Team!.Name))
                .Distinct()
                .ToListAsync();
            return Results.Ok(teams);
        });

        managerGroup.MapGet("/availability", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await ClaimPendingAsync(db, userId);
            var now = DateTimeOffset.UtcNow;

            var teamIds = await db.ManagerAssignments
                .Where(a => a.Person!.UserId == userId)
                .Select(a => a.TeamId)
                .Distinct()
                .ToListAsync();

            var result = new List<ManagerTeamAvailabilityDto>();
            foreach (var teamId in teamIds)
            {
                var team = await db.Teams.FirstAsync(t => t.Id == teamId);
                var members = await db.TeamMembers
                    .Where(m => m.TeamId == teamId && m.Status == EmployeeStatus.Active)
                    .Include(m => m.Person)
                    .Include(m => m.Track)
                    .OrderBy(m => m.Person!.Name)
                    .ToListAsync();

                var memberDtos = members.Select(m => new TeamMemberAvailabilityDto(
                    m.Id, m.PersonId, m.Person!.Name, m.Code, m.Track?.Name,
                    AvailabilityService.IsEffectivelyAvailable(m, m.Person!, now), m.AvailableSince, m.Person!.Timezone)).ToList();

                result.Add(new ManagerTeamAvailabilityDto(teamId, team.Name, memberDtos));
            }

            return Results.Ok(result.OrderBy(t => t.TeamName));
        });
    }

    // A manager-only person (never a TeamMember anywhere) has no team-scoped endpoint
    // to trigger claiming on first login, so these cross-team routes do it themselves —
    // same as GET /api/teams/mine does for the equivalent "before any team is picked" case.
    private static async Task ClaimPendingAsync(AppDbContext db, string userId)
    {
        var account = await db.Users.Where(u => u.Id == userId).Select(u => new { u.Email, u.PhoneNumber }).FirstOrDefaultAsync();
        if (account is not null)
            await PendingInviteClaimer.ClaimAsync(db, userId, account.Email, account.PhoneNumber);
    }
}
