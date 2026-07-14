using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class LocationsEndpoints
{
    public static void MapLocationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/locations").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Locations.Where(l => l.TeamId == teamId).OrderBy(l => l.Name).ToListAsync();
        }).RequireTeamMember();

        group.MapPost("", async (LocationDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Location name is required." });

            var exists = await db.Locations.AnyAsync(l => l.TeamId == teamId && l.Name.ToLower() == name.ToLower());
            if (exists)
                return Results.Conflict(new { message = $"'{name}' is already in the list." });

            var location = new Location { TeamId = teamId, Name = name };
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            return Results.Created($"/api/locations/{location.Id}", location);
        }).RequireTeamEditor();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == id && l.TeamId == teamId);
            if (location is null) return Results.NotFound();
            db.Locations.Remove(location);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }
}
