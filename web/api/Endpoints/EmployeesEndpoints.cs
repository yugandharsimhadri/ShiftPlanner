using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class EmployeesEndpoints
{
    public static void MapEmployeesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/employees").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Employees.Where(e => e.TeamId == teamId)
                .Include(e => e.Track).Include(e => e.Subtrack)
                .OrderBy(e => e.Name).ToListAsync();
        }).RequireTeamMember();

        group.MapGet("/next-code", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return Results.Ok(new { code = await SuggestNextEmployeeCode(db, teamId) });
        }).RequireTeamMember();

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Employees.Where(e => e.TeamId == teamId)
                .Include(e => e.Track).Include(e => e.Subtrack)
                .FirstOrDefaultAsync(e => e.Id == id)
                is { } emp ? Results.Ok(emp) : Results.NotFound();
        }).RequireTeamMember();

        group.MapPost("", async (EmployeeDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;

            var code = dto.Code.Trim();
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { message = "Employee code is required." });
            if (await db.Employees.AnyAsync(e => e.TeamId == teamId && e.Code == code))
                return Results.Conflict(new { message = $"Employee code '{code}' is already in use on this team." });

            var employee = new Employee
            {
                TeamId = teamId,
                Code = code,
                Name = dto.Name,
                Phone = dto.Phone,
                Email = dto.Email,
                TrackId = dto.TrackId,
                SubtrackId = dto.SubtrackId,
                Role = dto.Role,
                EmploymentType = dto.EmploymentType,
                JoinDate = dto.JoinDate,
                Status = dto.Status,
                Notes = dto.Notes
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return Results.Created($"/api/employees/{employee.Id}", employee);
        }).RequireTeamEditor();

        group.MapPut("/{id:guid}", async (Guid id, EmployeeDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId);
            if (employee is null) return Results.NotFound();

            var code = dto.Code.Trim();
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { message = "Employee code is required." });
            if (code != employee.Code && await db.Employees.AnyAsync(e => e.TeamId == teamId && e.Code == code))
                return Results.Conflict(new { message = $"Employee code '{code}' is already in use on this team." });

            employee.Code = code;
            employee.Name = dto.Name;
            employee.Phone = dto.Phone;
            employee.Email = dto.Email;
            employee.TrackId = dto.TrackId;
            employee.SubtrackId = dto.SubtrackId;
            employee.Role = dto.Role;
            employee.EmploymentType = dto.EmploymentType;
            employee.JoinDate = dto.JoinDate;
            employee.Status = dto.Status;
            employee.Notes = dto.Notes;

            await db.SaveChangesAsync();
            return Results.Ok(employee);
        }).RequireTeamEditor();

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId);
            if (employee is null) return Results.NotFound();
            db.Employees.Remove(employee);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }

    // Suggests the next sequential code (e.g. "EMP-004") for the new-employee form
    // to pre-fill — the admin can still type over it, since Code is editable.
    public static async Task<string> SuggestNextEmployeeCode(AppDbContext db, int teamId)
    {
        var codes = await db.Employees.Where(e => e.TeamId == teamId).Select(e => e.Code).ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            var parts = code.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var n) && n > max)
                max = n;
        }
        return $"EMP-{(max + 1):D3}";
    }
}
