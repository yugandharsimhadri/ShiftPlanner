using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class RosterEndpoints
{
    public static void MapRosterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/roster").RequireAuthorization();

        // Returns everything the grid needs in one call: entries, team members, tracks,
        // subtracks, holidays, approved leave (for shift-context clarity), and whether
        // this month has been published yet. Viewers get an empty Entries list for an
        // unpublished month — Editors/Admins always see the full draft.
        group.MapGet("", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var teamId = ctx.TeamId;
            var start = new DateOnly(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var status = await db.RosterMonthStatuses
                .FirstOrDefaultAsync(r => r.TeamId == teamId && r.Year == year && r.Month == month);
            var isPublished = status?.IsPublished ?? false;
            var canSeeDraft = ctx.Role is TeamRole.Editor or TeamRole.Admin;

            var entries = !isPublished && !canSeeDraft
                ? new List<RosterEntry>()
                : await db.RosterEntries
                    .Where(r => r.TeamId == teamId && r.Date >= start && r.Date <= end)
                    .ToListAsync();

            var teamMembers = await db.TeamMembers
                .Where(m => m.TeamId == teamId)
                .Include(m => m.Person)
                .Include(m => m.Track)
                .Include(m => m.Subtrack)
                .OrderBy(m => m.Person!.Name)
                .ToListAsync();

            var tracks = await db.Tracks.Where(t => t.TeamId == teamId).Include(t => t.Subtracks).OrderBy(t => t.Name).ToListAsync();
            var shiftTypes = await db.ShiftTypes.Where(s => s.TeamId == teamId).OrderBy(s => s.Code).ToListAsync();
            var holidays = await db.Holidays.Where(h => h.TeamId == teamId && h.Date >= start && h.Date <= end).ToListAsync();
            var team = await db.Teams.FirstAsync(t => t.Id == teamId);

            var leaveRequests = await db.LeaveRequests
                .Where(l => l.TeamId == teamId && l.Status == LeaveStatus.Approved && l.StartDate <= end && l.EndDate >= start)
                .Include(l => l.TeamMember!.Person)
                .ToListAsync();

            return Results.Ok(new
            {
                Year = year,
                Month = month,
                Entries = entries,
                TeamMembers = teamMembers,
                Tracks = tracks,
                ShiftTypes = shiftTypes,
                Holidays = holidays,
                DefaultOffDays = team.DefaultOffDays,
                LeaveRequests = leaveRequests.Select(ToLeaveDto).ToList(),
                IsPublished = isPublished,
            });
        }).RequireTeamMember();

        group.MapPut("/entry", async (RosterEntryUpsertDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var (entry, error) = await UpsertEntryAsync(db, ctx.TeamId, dto.TeamMemberId, dto.Date, dto.ShiftCode, dto.Note, ctx.UserId);
            if (error is not null) return Results.BadRequest(new { message = error });

            await db.SaveChangesAsync();
            return Results.Ok(entry);
        }).RequireTeamEditor();

        // Assigns the same shift code to every (member, date) combination in the
        // cross-product of TeamMemberIds x Dates. Errors (e.g. an inactive member) are
        // collected per-row rather than aborting the whole batch, since a bulk action
        // covering many people/days shouldn't fail entirely over one bad row.
        group.MapPost("/bulk-entry", async (BulkRosterEntryRequest req, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var errors = new List<string>();
            var updated = 0;

            foreach (var teamMemberId in req.TeamMemberIds)
            {
                foreach (var date in req.Dates)
                {
                    var (_, error) = await UpsertEntryAsync(db, ctx.TeamId, teamMemberId, date, req.ShiftCode, null, ctx.UserId, "BulkAssign");
                    if (error is not null) errors.Add(error);
                    else updated++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new BulkEntryResultDto(updated, errors));
        }).RequireTeamEditor();

        // Applies a per-weekday shift pattern across a whole month for the given members
        // — a scoped-down alternative to a full rotation/cycle engine. Members marked
        // Inactive are skipped (not hard-failed) when SkipInactive is set, matching the
        // same soft-skip behavior Copy Forward already uses.
        group.MapPost("/apply-pattern", async (ApplyPatternRequest req, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var start = new DateOnly(req.Year, req.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var members = await db.TeamMembers
                .Where(m => m.TeamId == ctx.TeamId && req.TeamMemberIds.Contains(m.Id))
                .ToListAsync();

            var errors = new List<string>();
            var updated = 0;

            foreach (var member in members)
            {
                if (req.SkipInactive && member.Status == EmployeeStatus.Inactive) continue;

                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    if (!req.WeeklyPattern.TryGetValue(date.DayOfWeek, out var shiftCode)) continue;

                    var (_, error) = await UpsertEntryAsync(db, ctx.TeamId, member.Id, date, shiftCode, null, ctx.UserId, "Pattern");
                    if (error is not null) errors.Add(error);
                    else updated++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new BulkEntryResultDto(updated, errors));
        }).RequireTeamEditor();

        group.MapGet("/publish-status", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var status = await db.RosterMonthStatuses
                .FirstOrDefaultAsync(r => r.TeamId == teamId && r.Year == year && r.Month == month);
            return Results.Ok(new RosterPublishStatusDto(status?.IsPublished ?? false, status?.PublishedAt));
        }).RequireTeamMember();

        group.MapPost("/publish", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var status = await db.RosterMonthStatuses
                .FirstOrDefaultAsync(r => r.TeamId == ctx.TeamId && r.Year == year && r.Month == month);

            if (status is null)
            {
                status = new RosterMonthStatus { TeamId = ctx.TeamId, Year = year, Month = month };
                db.RosterMonthStatuses.Add(status);
            }

            status.IsPublished = true;
            status.PublishedAt = DateTimeOffset.UtcNow;
            status.PublishedByUserId = ctx.UserId;
            await db.SaveChangesAsync();

            return Results.Ok(new RosterPublishStatusDto(true, status.PublishedAt));
        }).RequireTeamEditor();

        group.MapPost("/unpublish", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var status = await db.RosterMonthStatuses
                .FirstOrDefaultAsync(r => r.TeamId == teamId && r.Year == year && r.Month == month);
            if (status is null) return Results.Ok(new RosterPublishStatusDto(false, null));

            status.IsPublished = false;
            status.PublishedAt = null;
            await db.SaveChangesAsync();

            return Results.Ok(new RosterPublishStatusDto(false, null));
        }).RequireTeamEditor();

        group.MapGet("/history", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var start = new DateOnly(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            // SQLite/EF Core can't translate an OrderBy on DateTimeOffset into SQL — sort
            // client-side after materializing instead.
            var rows = (await db.RosterEntryHistories
                .Where(h => h.TeamId == teamId && h.Date >= start && h.Date <= end)
                .Include(h => h.TeamMember!.Person)
                .ToListAsync())
                .OrderByDescending(h => h.ChangedAt)
                .ToList();

            var dtos = rows.Select(h => new RosterEntryHistoryDto(
                h.Id, h.TeamMemberId, h.TeamMember?.Person?.Name ?? h.TeamMember?.Code ?? "Unknown",
                h.Date, h.OldShiftCode, h.NewShiftCode, h.ChangedByUserId, h.ChangedAt, h.Source));

            return Results.Ok(dtos);
        }).RequireTeamEditor();

        // A member acknowledges their own upcoming shift — a "seen it" signal, not a
        // requirement. Anyone can only acknowledge an entry that belongs to their own
        // TeamMember record.
        group.MapPatch("/entry/{id:int}/acknowledge", async (int id, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var entry = await db.RosterEntries
                .Include(e => e.TeamMember!.Person)
                .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == ctx.TeamId);
            if (entry is null) return Results.NotFound();

            if (entry.TeamMember?.Person?.UserId != ctx.UserId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            entry.AcknowledgedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(entry);
        }).RequireTeamMember();

        group.MapPost("/copy-forward", async (CopyForwardRequest req, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;

            var sourceStart = new DateOnly(req.SourceYear, req.SourceMonth, 1);
            var sourceEnd = sourceStart.AddMonths(1).AddDays(-1);
            var targetStart = new DateOnly(req.TargetYear, req.TargetMonth, 1);
            var targetEnd = targetStart.AddMonths(1).AddDays(-1);

            var sourceEntries = await db.RosterEntries
                .Where(r => r.TeamId == teamId && r.Date >= sourceStart && r.Date <= sourceEnd)
                .ToListAsync();

            var members = await db.TeamMembers.Where(m => m.TeamId == teamId).Include(m => m.Person).ToDictionaryAsync(m => m.Id);
            var holidays = (await db.Holidays
                .Where(h => h.TeamId == teamId && h.Date >= targetStart && h.Date <= targetEnd)
                .ToListAsync())
                .Select(h => h.Date)
                .ToHashSet();
            var workShiftCodes = await db.ShiftTypes
                .Where(s => s.TeamId == teamId && s.IsWorkShift)
                .Select(s => s.Code)
                .ToListAsync();
            var workShiftCodeSet = new HashSet<string>(workShiftCodes, StringComparer.OrdinalIgnoreCase);

            var flagged = new List<CopyForwardFlag>();
            var copiedCount = 0;

            foreach (var src in sourceEntries)
            {
                var targetDate = MapDate(src.Date, sourceStart, targetStart, req.Pattern);
                if (targetDate is null || targetDate < targetStart || targetDate > targetEnd)
                    continue; // no valid corresponding date this month (e.g. 5th Monday doesn't exist)

                if (!members.TryGetValue(src.TeamMemberId, out var member))
                    continue; // team member no longer on this team

                var reasons = new List<string>();

                if (member.Status == EmployeeStatus.Inactive)
                {
                    if (req.SkipInactive) continue;
                    reasons.Add("inactive-member");
                }

                if (holidays.Contains(targetDate.Value))
                    reasons.Add("holiday");

                var existing = await db.RosterEntries
                    .FirstOrDefaultAsync(r => r.TeamMemberId == src.TeamMemberId && r.Date == targetDate.Value);

                if (existing is null)
                {
                    db.RosterEntries.Add(new RosterEntry
                    {
                        TeamId = teamId,
                        TeamMemberId = src.TeamMemberId,
                        Date = targetDate.Value,
                        ShiftCode = src.ShiftCode,
                        Source = RosterEntrySource.Copied,
                        Note = src.Note
                    });
                }
                else
                {
                    existing.ShiftCode = src.ShiftCode;
                    existing.Source = RosterEntrySource.Copied;
                    existing.Note = src.Note;
                }

                var isWorkShift = src.ShiftCode is not null && workShiftCodeSet.Contains(src.ShiftCode);
                await CompOffAutoEarn.SyncAsync(db, teamId, src.TeamMemberId, targetDate.Value, isWorkShift);

                copiedCount++;

                foreach (var reason in reasons)
                    flagged.Add(new CopyForwardFlag(member.Id, member.Person!.Name, targetDate.Value, reason));
            }

            await db.SaveChangesAsync();
            return Results.Ok(new CopyForwardResult(copiedCount, flagged));
        }).RequireTeamEditor();
    }

    // Single shared path for every roster-cell write (manual entry, bulk assign, apply
    // pattern, and shift-swap approval) — keeps the inactive-member guard, comp-off sync,
    // and change-history logging consistent no matter which feature touched the cell.
    // Does not call SaveChangesAsync — callers batch their own save. Internal (not
    // private) so ShiftSwapsEndpoints can reuse it when a swap is approved.
    internal static async Task<(RosterEntry? Entry, string? Error)> UpsertEntryAsync(
        AppDbContext db, int teamId, int teamMemberId, DateOnly date, string? shiftCode, string? note,
        string changedByUserId, string source = "Manual")
    {
        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == teamMemberId && m.TeamId == teamId);
        if (member is null) return (null, $"Team member '{teamMemberId}' not found.");

        if (member.Status == EmployeeStatus.Inactive)
            return (null, $"'{member.Code}' is inactive and can't be assigned a shift.");

        var shiftType = shiftCode is null
            ? null
            : await db.ShiftTypes.FirstOrDefaultAsync(s => s.TeamId == teamId && s.Code == shiftCode);
        if (shiftCode is not null && shiftType is null)
            return (null, $"Shift type '{shiftCode}' not found.");

        var entry = await db.RosterEntries.FirstOrDefaultAsync(r => r.TeamMemberId == teamMemberId && r.Date == date);
        var oldCode = entry?.ShiftCode;

        if (entry is null)
        {
            entry = new RosterEntry
            {
                TeamId = teamId,
                TeamMemberId = teamMemberId,
                Date = date,
                ShiftCode = shiftCode,
                Source = RosterEntrySource.Manual,
                Note = note,
            };
            db.RosterEntries.Add(entry);
        }
        else
        {
            entry.ShiftCode = shiftCode;
            entry.Source = RosterEntrySource.Manual;
            entry.Note = note;
            if (oldCode != shiftCode) entry.AcknowledgedAt = null;
        }

        if (oldCode != shiftCode)
        {
            db.RosterEntryHistories.Add(new RosterEntryHistory
            {
                TeamId = teamId,
                TeamMemberId = teamMemberId,
                Date = date,
                OldShiftCode = oldCode,
                NewShiftCode = shiftCode,
                ChangedByUserId = changedByUserId,
                Source = source,
            });
        }

        await CompOffAutoEarn.SyncAsync(db, teamId, teamMemberId, date, shiftType?.IsWorkShift == true);

        return (entry, null);
    }

    private static LeaveRequestDto ToLeaveDto(LeaveRequest l) => new(
        l.Id, l.TeamMemberId, l.TeamMember!.Person!.Name, l.TeamMember.Code,
        l.StartDate, l.EndDate, l.Reason, l.Status, l.RequestedAt, l.DecidedAt, l.DecisionNote);

    private static DateOnly? MapDate(DateOnly sourceDate, DateOnly sourceMonthStart, DateOnly targetMonthStart, string pattern)
    {
        var targetMonthEnd = targetMonthStart.AddMonths(1).AddDays(-1);

        if (pattern == "exact-date")
        {
            var day = Math.Min(sourceDate.Day, targetMonthEnd.Day);
            return new DateOnly(targetMonthStart.Year, targetMonthStart.Month, day);
        }

        // "weekday" pattern: preserve the Nth occurrence of the weekday within the month
        // e.g. the 2nd Tuesday of the source month maps to the 2nd Tuesday of the target month.
        var weekday = sourceDate.DayOfWeek;
        var occurrence = (sourceDate.Day - 1) / 7 + 1; // 1-based occurrence within the month

        var candidate = new DateOnly(targetMonthStart.Year, targetMonthStart.Month, 1);
        var firstOccurrenceOffset = ((int)weekday - (int)candidate.DayOfWeek + 7) % 7;
        var firstOccurrenceDate = candidate.AddDays(firstOccurrenceOffset);
        var targetDate = firstOccurrenceDate.AddDays((occurrence - 1) * 7);

        if (targetDate.Month != targetMonthStart.Month) return null; // occurrence doesn't exist this month
        return targetDate;
    }
}
