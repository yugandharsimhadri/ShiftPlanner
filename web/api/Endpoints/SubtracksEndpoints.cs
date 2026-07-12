using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class SubtracksEndpoints
{
    public static void MapSubtracksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/subtracks").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Subtracks.Where(s => s.TeamId == teamId).OrderBy(s => s.Name).ToListAsync();
        }).RequireTeamMember();

        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Subtracks.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId) is { } sub
                ? Results.Ok(sub) : Results.NotFound();
        }).RequireTeamMember();

        group.MapPost("", async (SubtrackDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == dto.TrackId && t.TeamId == teamId);
            if (track is null) return Results.BadRequest(new { message = "Track not found on this team." });

            var sub = new Subtrack { TeamId = teamId, TrackId = dto.TrackId, Name = dto.Name };
            db.Subtracks.Add(sub);
            await db.SaveChangesAsync();
            return Results.Created($"/api/subtracks/{sub.Id}", sub);
        }).RequireTeamEditor();

        group.MapPut("/{id:int}", async (int id, SubtrackDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var sub = await db.Subtracks.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId);
            if (sub is null) return Results.NotFound();

            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == dto.TrackId && t.TeamId == teamId);
            if (track is null) return Results.BadRequest(new { message = "Track not found on this team." });

            sub.TrackId = dto.TrackId;
            sub.Name = dto.Name;
            await db.SaveChangesAsync();
            return Results.Ok(sub);
        }).RequireTeamEditor();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var sub = await db.Subtracks.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId);
            if (sub is null) return Results.NotFound();
            db.Subtracks.Remove(sub);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }
}
