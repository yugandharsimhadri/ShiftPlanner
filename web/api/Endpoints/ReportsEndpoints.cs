using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports").RequireAuthorization();

        // One row per active team member for the given date range: total shifts worked,
        // how many of those fell on a default-off day, and their comp-off standing.
        // CompOffsEarned/Used are scoped to the date range; CompOffsPending is the
        // member's current balance regardless of range, since "who's owed a comp-off
        // right now" doesn't care when it was earned.
        group.MapGet("/utilization", async (DateOnly start, DateOnly end, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            if (end < start) return Results.BadRequest(new { message = "End date must be on or after the start date." });

            var team = await db.Teams.FirstAsync(t => t.Id == teamId);
            var offDays = team.DefaultOffDays;

            var workShiftCodes = new HashSet<string>(
                await db.ShiftTypes.Where(s => s.TeamId == teamId && s.IsWorkShift).Select(s => s.Code).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            var members = await db.TeamMembers
                .Where(m => m.TeamId == teamId && m.Status == EmployeeStatus.Active)
                .Include(m => m.Person)
                .Include(m => m.Track)
                .OrderBy(m => m.Person!.Name)
                .ToListAsync();

            var entries = await db.RosterEntries
                .Where(r => r.TeamId == teamId && r.Date >= start && r.Date <= end && r.ShiftCode != null)
                .ToListAsync();
            var entriesByMember = entries.ToLookup(e => e.TeamMemberId);

            var compOffs = await db.CompOffEntries.Where(c => c.TeamId == teamId).ToListAsync();
            var compOffsByMember = compOffs.ToLookup(c => c.TeamMemberId);

            var rows = members.Select(member =>
            {
                var myEntries = entriesByMember[member.Id].Where(e => workShiftCodes.Contains(e.ShiftCode!)).ToList();
                var weekendCount = myEntries.Count(e => offDays.Contains(e.Date.DayOfWeek));
                var myCompOffs = compOffsByMember[member.Id].ToList();

                return new UtilizationRowDto(
                    member.Id,
                    member.Code,
                    member.Person!.Name,
                    member.Track?.Name,
                    myEntries.Count,
                    weekendCount,
                    myCompOffs.Count(c => c.EarnedDate >= start && c.EarnedDate <= end),
                    myCompOffs.Count(c => c.Status == CompOffStatus.Used && c.UsedDate >= start && c.UsedDate <= end),
                    myCompOffs.Count(c => c.Status == CompOffStatus.Pending)
                );
            })
            .OrderByDescending(r => r.WeekendShiftsWorked)
            .ThenByDescending(r => r.TotalShiftsWorked)
            .ToList();

            return Results.Ok(rows);
        }).RequireTeamMember();
    }
}
