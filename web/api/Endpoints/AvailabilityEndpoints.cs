using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class AvailabilityEndpoints
{
    public static void MapAvailabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teams/current").RequireAuthorization();

        // Every active member's live "free right now" status — independent of the
        // planned roster. Visible to any team member, not just admins.
        group.MapGet("/availability", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var now = DateTimeOffset.UtcNow;

            var members = await db.TeamMembers
                .Where(m => m.TeamId == teamId && m.Status == Models.EmployeeStatus.Active)
                .Include(m => m.Person)
                .Include(m => m.Track)
                .OrderBy(m => m.Person!.Name)
                .ToListAsync();

            var result = members.Select(m => ToDto(m, m.Person!, now)).ToList();
            return Results.Ok(result);
        }).RequireTeamMember();

        // Self-toggle. A member can only flip their own status.
        group.MapPatch("/members/me/availability", async (UpdateAvailabilityDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers
                .Include(m => m.Person)
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Person!.UserId == ctx.UserId);
            if (member is null) return Results.NotFound();

            member.IsAvailable = dto.IsAvailable;
            member.AvailableSince = dto.IsAvailable ? DateTimeOffset.UtcNow : null;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(member, member.Person!, DateTimeOffset.UtcNow));
        }).RequireTeamMember();
    }

    private static TeamMemberAvailabilityDto ToDto(Models.TeamMember m, Models.Person p, DateTimeOffset now) => new(
        m.Id, p.Id, p.Name, m.Code, m.Track?.Name,
        AvailabilityService.IsEffectivelyAvailable(m, p, now), m.AvailableSince, p.Timezone);
}
