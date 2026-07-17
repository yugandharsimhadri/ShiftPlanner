using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

// A one-directional "can someone take my shift" offer — not a mutual two-way trade. See
// ShiftSwapRequest for the full reasoning. Offer -> (optional) claim -> Editor/Admin
// approval is what actually reassigns the roster entry, via RosterEndpoints.UpsertEntryAsync.
public static class ShiftSwapsEndpoints
{
    public static void MapShiftSwapsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shift-swaps").RequireAuthorization();

        // Editor/Admin see every request on the team. Everyone else sees: open offers
        // they're eligible to claim (untargeted, or targeted at them), plus their own
        // history as either the offerer or the claimant.
        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var query = db.ShiftSwapRequests.Where(s => s.TeamId == ctx.TeamId)
                .Include(s => s.OfferedByTeamMember!.Person)
                .Include(s => s.TargetTeamMember!.Person)
                .Include(s => s.ClaimedByTeamMember!.Person)
                .AsQueryable();

            if (ctx.Role is not (TeamRole.Editor or TeamRole.Admin))
            {
                var me = await db.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Person!.UserId == ctx.UserId);
                var myId = me?.Id ?? -1;
                query = query.Where(s =>
                    (s.Status == SwapStatus.Open && (s.TargetTeamMemberId == null || s.TargetTeamMemberId == myId)) ||
                    s.OfferedByTeamMemberId == myId ||
                    s.ClaimedByTeamMemberId == myId);
            }

            // SQLite/EF Core can't translate an OrderBy on DateTimeOffset into SQL — sort
            // client-side after materializing instead.
            var list = (await query.ToListAsync()).OrderByDescending(s => s.CreatedAt).ToList();
            return Results.Ok(list.Select(ToDto));
        }).RequireTeamMember();

        group.MapPost("", async (CreateShiftSwapDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers.Include(m => m.Person)
                .FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Person!.UserId == ctx.UserId);
            if (member is null) return Results.NotFound();

            var entry = await db.RosterEntries
                .FirstOrDefaultAsync(r => r.TeamMemberId == member.Id && r.Date == dto.Date);
            if (entry is null || entry.ShiftCode != dto.ShiftCode)
                return Results.BadRequest(new { message = "You don't have that shift assigned on that date." });

            if (dto.TargetTeamMemberId is { } targetId)
            {
                var targetExists = await db.TeamMembers.AnyAsync(m => m.Id == targetId && m.TeamId == ctx.TeamId);
                if (!targetExists) return Results.BadRequest(new { message = "That team member wasn't found." });
            }

            var swap = new ShiftSwapRequest
            {
                TeamId = ctx.TeamId,
                OfferedByTeamMemberId = member.Id,
                Date = dto.Date,
                ShiftCode = dto.ShiftCode,
                TargetTeamMemberId = dto.TargetTeamMemberId,
            };
            db.ShiftSwapRequests.Add(swap);
            await db.SaveChangesAsync();

            swap.OfferedByTeamMember = member;
            return Results.Ok(ToDto(swap));
        }).RequireTeamMember();

        group.MapPost("/{id:int}/claim", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var swap = await LoadSwap(db, id, ctx.TeamId);
            if (swap is null) return Results.NotFound();
            if (swap.Status != SwapStatus.Open)
                return Results.BadRequest(new { message = "That offer isn't open anymore." });

            var member = await db.TeamMembers.Include(m => m.Person)
                .FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Person!.UserId == ctx.UserId);
            if (member is null) return Results.NotFound();
            if (member.Id == swap.OfferedByTeamMemberId)
                return Results.BadRequest(new { message = "You can't claim your own offer." });
            if (swap.TargetTeamMemberId is { } targetId && targetId != member.Id)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            swap.ClaimedByTeamMemberId = member.Id;
            swap.Status = SwapStatus.Claimed;
            swap.RespondedAt = DateTimeOffset.UtcNow;
            swap.ClaimedByTeamMember = member;

            // Auto-approve is on by default — most teams don't want to sit as a
            // bottleneck between "someone claimed this" and it actually moving on the
            // roster. DecidedByUserId stays null since no person actually made the call.
            var team = await db.Teams.FirstAsync(t => t.Id == ctx.TeamId);
            if (team.AutoApproveShiftSwaps)
            {
                var error = await FinalizeSwapAsync(db, ctx.TeamId, swap, decidedByUserId: null);
                if (error is not null) return Results.BadRequest(new { message = error });
            }

            await db.SaveChangesAsync();
            return Results.Ok(ToDto(swap));
        }).RequireTeamMember();

        group.MapPost("/{id:int}/approve", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var swap = await LoadSwap(db, id, ctx.TeamId);
            if (swap is null) return Results.NotFound();
            if (swap.Status != SwapStatus.Claimed || swap.ClaimedByTeamMemberId is null)
                return Results.BadRequest(new { message = "This offer needs to be claimed before it can be approved." });

            var error = await FinalizeSwapAsync(db, ctx.TeamId, swap, ctx.UserId);
            if (error is not null) return Results.BadRequest(new { message = error });
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(swap));
        }).RequireTeamEditor();

        group.MapPost("/{id:int}/reject", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var swap = await LoadSwap(db, id, ctx.TeamId);
            if (swap is null) return Results.NotFound();
            if (swap.Status is SwapStatus.Approved or SwapStatus.Rejected or SwapStatus.Cancelled)
                return Results.BadRequest(new { message = "That offer has already been settled." });

            swap.Status = SwapStatus.Rejected;
            swap.DecidedByUserId = ctx.UserId;
            swap.DecidedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(swap));
        }).RequireTeamEditor();

        group.MapPost("/{id:int}/cancel", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var swap = await LoadSwap(db, id, ctx.TeamId);
            if (swap is null) return Results.NotFound();
            if (swap.OfferedByTeamMember?.Person?.UserId != ctx.UserId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (swap.Status is SwapStatus.Approved or SwapStatus.Rejected or SwapStatus.Cancelled)
                return Results.BadRequest(new { message = "That offer has already been settled." });

            swap.Status = SwapStatus.Cancelled;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(swap));
        }).RequireTeamMember();
    }

    // Moves the roster entry from the offerer to the claimant and marks the swap
    // Approved. Shared by the manual /approve endpoint and by /claim when the team has
    // auto-approve turned on — does not call SaveChangesAsync, callers batch their own.
    private static async Task<string?> FinalizeSwapAsync(AppDbContext db, int teamId, ShiftSwapRequest swap, string? decidedByUserId)
    {
        var (_, clearError) = await RosterEndpoints.UpsertEntryAsync(
            db, teamId, swap.OfferedByTeamMemberId, swap.Date, null, null, decidedByUserId ?? "system:auto-approve", "SwapApproved");
        if (clearError is not null) return clearError;

        var (_, assignError) = await RosterEndpoints.UpsertEntryAsync(
            db, teamId, swap.ClaimedByTeamMemberId!.Value, swap.Date, swap.ShiftCode, null, decidedByUserId ?? "system:auto-approve", "SwapApproved");
        if (assignError is not null) return assignError;

        swap.Status = SwapStatus.Approved;
        swap.DecidedByUserId = decidedByUserId;
        swap.DecidedAt = DateTimeOffset.UtcNow;
        return null;
    }

    private static Task<ShiftSwapRequest?> LoadSwap(AppDbContext db, int id, int teamId) =>
        db.ShiftSwapRequests
            .Include(s => s.OfferedByTeamMember!.Person)
            .Include(s => s.TargetTeamMember!.Person)
            .Include(s => s.ClaimedByTeamMember!.Person)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId);

    private static ShiftSwapRequestDto ToDto(ShiftSwapRequest s) => new(
        s.Id, s.OfferedByTeamMemberId, s.OfferedByTeamMember!.Person!.Name, s.Date, s.ShiftCode,
        s.TargetTeamMemberId, s.TargetTeamMember?.Person?.Name,
        s.ClaimedByTeamMemberId, s.ClaimedByTeamMember?.Person?.Name,
        s.Status, s.CreatedAt);
}
