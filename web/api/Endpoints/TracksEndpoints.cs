using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class TracksEndpoints
{
    public static void MapTracksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tracks").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Tracks.Where(t => t.TeamId == teamId).Include(t => t.Subtracks).OrderBy(t => t.Name).ToListAsync();
        }).RequireTeamMember();

        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Tracks.Where(t => t.TeamId == teamId).Include(t => t.Subtracks).FirstOrDefaultAsync(t => t.Id == id)
                is { } track ? Results.Ok(track) : Results.NotFound();
        }).RequireTeamMember();

        group.MapPost("", async (TrackDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var track = new Track { TeamId = teamId, Name = dto.Name, Color = dto.Color };
            db.Tracks.Add(track);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tracks/{track.Id}", track);
        }).RequireTeamEditor();

        group.MapPut("/{id:int}", async (int id, TrackDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id && t.TeamId == teamId);
            if (track is null) return Results.NotFound();
            track.Name = dto.Name;
            track.Color = dto.Color;
            await db.SaveChangesAsync();
            return Results.Ok(track);
        }).RequireTeamEditor();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id && t.TeamId == teamId);
            if (track is null) return Results.NotFound();
            db.Tracks.Remove(track);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }
}
