using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Services;

// Keeps the comp-off ledger in sync with a single roster entry. Called from both the
// day-by-day entry upsert and copy-forward, so working (or un-working) a default-off day
// earns — or un-earns — a comp-off consistently no matter which path touched the roster.
// Does not call SaveChangesAsync — the caller is expected to save alongside its own changes.
public static class CompOffAutoEarn
{
    public static async Task SyncAsync(AppDbContext db, int teamId, Guid employeeId, DateOnly date, bool isWorkShift)
    {
        var team = await db.Teams.FirstAsync(t => t.Id == teamId);
        if (!team.CompOffsEnabled) return;

        var isDefaultOffDay = team.DefaultOffDays.Contains(date.DayOfWeek);
        var shouldEarn = isDefaultOffDay && isWorkShift;

        var existingPending = await db.CompOffEntries.FirstOrDefaultAsync(c =>
            c.TeamId == teamId && c.EmployeeId == employeeId && c.EarnedDate == date && c.Status == CompOffStatus.Pending);

        if (shouldEarn && existingPending is null)
        {
            db.CompOffEntries.Add(new CompOffEntry
            {
                TeamId = teamId,
                EmployeeId = employeeId,
                EarnedDate = date,
                Status = CompOffStatus.Pending,
            });
        }
        else if (!shouldEarn && existingPending is not null)
        {
            // The day no longer represents "worked a default-off day" (cleared, or changed
            // to a non-work code) — remove the still-unused comp-off it would have earned.
            // A Used comp-off for this date is left alone; it's already been spent.
            db.CompOffEntries.Remove(existingPending);
        }
    }
}
