using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class JobRolesEndpoints
{
    public static void MapJobRolesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/job-roles").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.JobRoles.Where(r => r.TeamId == teamId).OrderBy(r => r.Name).ToListAsync();
        }).RequireTeamMember();

        group.MapPost("", async (JobRoleDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Role name is required." });

            var exists = await db.JobRoles.AnyAsync(r => r.TeamId == teamId && r.Name.ToLower() == name.ToLower());
            if (exists)
                return Results.Conflict(new { message = $"'{name}' is already in the list." });

            var role = new JobRole { TeamId = teamId, Name = name };
            db.JobRoles.Add(role);
            await db.SaveChangesAsync();
            return Results.Created($"/api/job-roles/{role.Id}", role);
        }).RequireTeamEditor();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var role = await db.JobRoles.FirstOrDefaultAsync(r => r.Id == id && r.TeamId == teamId);
            if (role is null) return Results.NotFound();
            db.JobRoles.Remove(role);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }
}
