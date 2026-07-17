using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class LeaveRequestsEndpoints
{
    public static void MapLeaveRequestsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leave-requests").RequireAuthorization();

        // Viewers/Editors see only their own requests; Editor/Admin can additionally
        // filter by status to build an approval queue.
        group.MapGet("", async (string? status, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var query = db.LeaveRequests.Where(l => l.TeamId == ctx.TeamId).Include(l => l.TeamMember!.Person).AsQueryable();

            if (ctx.Role is not (TeamRole.Editor or TeamRole.Admin))
            {
                query = query.Where(l => l.TeamMember!.Person!.UserId == ctx.UserId);
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, true, out var parsed))
                query = query.Where(l => l.Status == parsed);

            // SQLite/EF Core can't translate an OrderBy on DateTimeOffset into SQL — sort
            // client-side after materializing instead.
            var list = (await query.ToListAsync()).OrderByDescending(l => l.RequestedAt).ToList();
            return Results.Ok(list.Select(ToDto));
        }).RequireTeamMember();

        group.MapPost("", async (CreateLeaveRequestDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers
                .Include(m => m.Person)
                .FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Person!.UserId == ctx.UserId);
            if (member is null) return Results.NotFound();

            if (dto.EndDate < dto.StartDate)
                return Results.BadRequest(new { message = "End date can't be before the start date." });

            var team = await db.Teams.FirstAsync(t => t.Id == ctx.TeamId);

            var request = new LeaveRequest
            {
                TeamId = ctx.TeamId,
                TeamMemberId = member.Id,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Reason = dto.Reason,
            };

            // Auto-approve is on by default — most teams don't want a manual approval
            // step for something a member can self-serve. DecidedByUserId stays null
            // here since no person actually made the call.
            if (team.AutoApproveLeaveRequests)
            {
                request.Status = LeaveStatus.Approved;
                request.DecidedAt = DateTimeOffset.UtcNow;
            }

            db.LeaveRequests.Add(request);
            await db.SaveChangesAsync();

            request.TeamMember = member;
            return Results.Ok(ToDto(request));
        }).RequireTeamMember();

        group.MapPost("/{id:int}/approve", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var request = await db.LeaveRequests.Include(l => l.TeamMember!.Person)
                .FirstOrDefaultAsync(l => l.Id == id && l.TeamId == ctx.TeamId);
            if (request is null) return Results.NotFound();
            if (request.Status != LeaveStatus.Pending)
                return Results.BadRequest(new { message = "That request has already been decided." });

            request.Status = LeaveStatus.Approved;
            request.DecidedByUserId = ctx.UserId;
            request.DecidedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(request));
        }).RequireTeamEditor();

        group.MapPost("/{id:int}/reject", async (int id, DecideLeaveRequestDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var request = await db.LeaveRequests.Include(l => l.TeamMember!.Person)
                .FirstOrDefaultAsync(l => l.Id == id && l.TeamId == ctx.TeamId);
            if (request is null) return Results.NotFound();
            if (request.Status != LeaveStatus.Pending)
                return Results.BadRequest(new { message = "That request has already been decided." });

            request.Status = LeaveStatus.Rejected;
            request.DecidedByUserId = ctx.UserId;
            request.DecidedAt = DateTimeOffset.UtcNow;
            request.DecisionNote = dto.DecisionNote;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(request));
        }).RequireTeamEditor();

        group.MapPost("/{id:int}/cancel", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var request = await db.LeaveRequests.Include(l => l.TeamMember!.Person)
                .FirstOrDefaultAsync(l => l.Id == id && l.TeamId == ctx.TeamId);
            if (request is null) return Results.NotFound();
            if (request.TeamMember?.Person?.UserId != ctx.UserId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.Status != LeaveStatus.Pending)
                return Results.BadRequest(new { message = "Only a pending request can be cancelled." });

            request.Status = LeaveStatus.Cancelled;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(request));
        }).RequireTeamMember();
    }

    private static LeaveRequestDto ToDto(LeaveRequest l) => new(
        l.Id, l.TeamMemberId, l.TeamMember!.Person!.Name, l.TeamMember.Code,
        l.StartDate, l.EndDate, l.Reason, l.Status, l.RequestedAt, l.DecidedAt, l.DecisionNote);
}
