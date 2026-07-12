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

        // Returns everything the grid needs in one call: entries, team members, tracks, subtracks.
        group.MapGet("", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var start = new DateOnly(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var entries = await db.RosterEntries
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
            });
        }).RequireTeamMember();

        group.MapPut("/entry", async (RosterEntryUpsertDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;

            var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == dto.TeamMemberId && m.TeamId == teamId);
            if (member is null) return Results.NotFound($"Team member '{dto.TeamMemberId}' not found.");

            var shiftType = dto.ShiftCode is null
                ? null
                : await db.ShiftTypes.FirstOrDefaultAsync(s => s.TeamId == teamId && s.Code == dto.ShiftCode);
            if (dto.ShiftCode is not null && shiftType is null)
                return Results.BadRequest($"Shift type '{dto.ShiftCode}' not found.");

            var entry = await db.RosterEntries
                .FirstOrDefaultAsync(r => r.TeamMemberId == dto.TeamMemberId && r.Date == dto.Date);

            if (entry is null)
            {
                entry = new RosterEntry
                {
                    TeamId = teamId,
                    TeamMemberId = dto.TeamMemberId,
                    Date = dto.Date,
                    ShiftCode = dto.ShiftCode,
                    Source = RosterEntrySource.Manual,
                    Note = dto.Note
                };
                db.RosterEntries.Add(entry);
            }
            else
            {
                entry.ShiftCode = dto.ShiftCode;
                entry.Source = RosterEntrySource.Manual;
                entry.Note = dto.Note;
            }

            await CompOffAutoEarn.SyncAsync(db, teamId, dto.TeamMemberId, dto.Date, shiftType?.IsWorkShift == true);

            await db.SaveChangesAsync();
            return Results.Ok(entry);
        }).RequireTeamEditor();

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
