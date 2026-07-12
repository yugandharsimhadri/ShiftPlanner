using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class CompOffsEndpoints
{
    public static void MapCompOffsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/compoffs").RequireAuthorization();

        // Optional ?status=Pending|Used filter, and ?employeeId= to scope to one person
        // (used by the Roster page when picking which pending comp-off a make-up day settles).
        group.MapGet("", async (string? status, Guid? employeeId, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;

            var query = db.CompOffEntries.Where(c => c.TeamId == teamId).Include(c => c.Employee).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CompOffStatus>(status, true, out var parsedStatus))
                query = query.Where(c => c.Status == parsedStatus);

            if (employeeId is { } id)
                query = query.Where(c => c.EmployeeId == id);

            var list = await query
                .OrderByDescending(c => c.EarnedDate)
                .Select(c => new CompOffEntryDto(c.Id, c.EmployeeId, c.Employee!.Code, c.Employee.Name, c.EarnedDate, c.Status, c.UsedDate))
                .ToListAsync();

            return Results.Ok(list);
        }).RequireTeamMember();

        // Marks a pending comp-off as used against a specific make-up day off.
        group.MapPost("/{id:int}/use", async (int id, UseCompOffDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var entry = await db.CompOffEntries.Include(c => c.Employee)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == teamId);
            if (entry is null) return Results.NotFound();
            if (entry.Status != CompOffStatus.Pending)
                return Results.BadRequest(new { message = "That comp-off has already been used." });

            entry.Status = CompOffStatus.Used;
            entry.UsedDate = dto.UsedDate;
            await db.SaveChangesAsync();

            return Results.Ok(new CompOffEntryDto(entry.Id, entry.EmployeeId, entry.Employee!.Code, entry.Employee.Name, entry.EarnedDate, entry.Status, entry.UsedDate));
        }).RequireTeamEditor();

        // Reverts a used comp-off back to pending, e.g. the make-up day plan changed.
        group.MapPost("/{id:int}/unuse", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var entry = await db.CompOffEntries.Include(c => c.Employee)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == teamId);
            if (entry is null) return Results.NotFound();

            entry.Status = CompOffStatus.Pending;
            entry.UsedDate = null;
            await db.SaveChangesAsync();

            return Results.Ok(new CompOffEntryDto(entry.Id, entry.EmployeeId, entry.Employee!.Code, entry.Employee.Name, entry.EarnedDate, entry.Status, entry.UsedDate));
        }).RequireTeamEditor();
    }
}
