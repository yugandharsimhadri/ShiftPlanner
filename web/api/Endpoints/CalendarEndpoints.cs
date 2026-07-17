using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Endpoints;

// A subscribable .ics feed of a person's own upcoming shifts, across every team they're
// on — so their shifts show up in whatever calendar app they already check daily, rather
// than requiring them to remember to open ShiftPlanner.
public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this WebApplication app)
    {
        var meGroup = app.MapGroup("/api/me").RequireAuthorization();

        meGroup.MapGet("/calendar-feed-url", async (AppDbContext db, ClaimsPrincipal user, HttpRequest request) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var person = await db.People.FirstOrDefaultAsync(p => p.UserId == userId);
            if (person is null) return Results.NotFound();

            if (person.CalendarFeedToken is null)
            {
                person.CalendarFeedToken = Guid.NewGuid();
                await db.SaveChangesAsync();
            }

            var baseUrl = $"{request.Scheme}://{request.Host}";
            return Results.Ok(new { url = $"{baseUrl}/api/calendar/{person.CalendarFeedToken}.ics" });
        });

        // Deliberately unauthenticated — the token itself is the credential, the same
        // trust model every subscribable calendar feed URL uses (a calendar app polling
        // this URL has no way to send an Authorization header).
        var publicGroup = app.MapGroup("/api/calendar");

        publicGroup.MapGet("/{token:guid}.ics", async (Guid token, AppDbContext db) =>
        {
            var person = await db.People.FirstOrDefaultAsync(p => p.CalendarFeedToken == token);
            if (person is null) return Results.NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var windowStart = today.AddDays(-7);
            var windowEnd = today.AddDays(60);

            var members = await db.TeamMembers
                .Where(m => m.PersonId == person.Id)
                .Include(m => m.Team)
                .ToListAsync();
            var memberById = members.ToDictionary(m => m.Id);
            var memberIds = members.Select(m => m.Id).ToList();

            var entries = await db.RosterEntries
                .Where(e => memberIds.Contains(e.TeamMemberId) && e.ShiftCode != null
                    && e.Date >= windowStart && e.Date <= windowEnd)
                .ToListAsync();

            var teamIds = members.Select(m => m.TeamId).Distinct().ToList();
            var shiftTypesByTeamAndCode = (await db.ShiftTypes.Where(s => teamIds.Contains(s.TeamId)).ToListAsync())
                .ToDictionary(s => (s.TeamId, s.Code));

            var ics = BuildIcs(entries, memberById, shiftTypesByTeamAndCode);
            return Results.Text(ics, "text/calendar");
        });
    }

    private static string BuildIcs(
        List<RosterEntry> entries,
        Dictionary<int, TeamMember> memberById,
        Dictionary<(int TeamId, string Code), ShiftType> shiftTypesByTeamAndCode)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//ShiftPlanner//Calendar Feed//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");

        foreach (var entry in entries)
        {
            if (!memberById.TryGetValue(entry.TeamMemberId, out var member)) continue;
            shiftTypesByTeamAndCode.TryGetValue((member.TeamId, entry.ShiftCode!), out var shiftType);

            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:shiftplanner-entry-{entry.Id}@shiftplanner.local\r\n");
            sb.Append($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}\r\n");

            if (shiftType is { Start: { } start, End: { } end })
            {
                var dtStart = entry.Date.ToDateTime(start);
                var endDate = shiftType.IsOvernight ? entry.Date.AddDays(1) : entry.Date;
                var dtEnd = endDate.ToDateTime(end);
                sb.Append($"DTSTART:{dtStart:yyyyMMddTHHmmss}\r\n");
                sb.Append($"DTEND:{dtEnd:yyyyMMddTHHmmss}\r\n");
            }
            else
            {
                sb.Append($"DTSTART;VALUE=DATE:{entry.Date:yyyyMMdd}\r\n");
                sb.Append($"DTEND;VALUE=DATE:{entry.Date.AddDays(1):yyyyMMdd}\r\n");
            }

            var summary = shiftType?.Name ?? entry.ShiftCode ?? "Shift";
            var teamName = member.Team?.Name ?? "";
            sb.Append($"SUMMARY:{EscapeIcsText($"{summary} — {teamName}")}\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static string EscapeIcsText(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
