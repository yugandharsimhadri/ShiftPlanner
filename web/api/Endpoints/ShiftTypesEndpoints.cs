using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class ShiftTypesEndpoints
{
    public static void MapShiftTypesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shift-types").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.ShiftTypes.Where(s => s.TeamId == teamId).OrderBy(s => s.Code).ToListAsync();
        }).RequireTeamMember();

        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.ShiftTypes.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId) is { } st
                ? Results.Ok(st) : Results.NotFound();
        }).RequireTeamMember();

        group.MapPost("", async (ShiftTypeDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var code = dto.Code.Trim();
            if (await db.ShiftTypes.AnyAsync(s => s.TeamId == teamId && s.Code == code))
                return Results.Conflict(new { message = $"Shift type '{code}' already exists on this team." });

            var st = new ShiftType
            {
                TeamId = teamId,
                Code = code,
                Name = dto.Name,
                Start = dto.Start,
                End = dto.End,
                Color = dto.Color,
                IsOvernight = dto.IsOvernight,
                IsWorkShift = dto.IsWorkShift
            };
            db.ShiftTypes.Add(st);
            await db.SaveChangesAsync();
            return Results.Created($"/api/shift-types/{st.Id}", st);
        }).RequireTeamEditor();

        group.MapPut("/{id:int}", async (int id, ShiftTypeDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var st = await db.ShiftTypes.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId);
            if (st is null) return Results.NotFound();

            var code = dto.Code.Trim();
            if (code != st.Code && await db.ShiftTypes.AnyAsync(s => s.TeamId == teamId && s.Code == code))
                return Results.Conflict(new { message = $"Shift type '{code}' already exists on this team." });

            st.Code = code;
            st.Name = dto.Name;
            st.Start = dto.Start;
            st.End = dto.End;
            st.Color = dto.Color;
            st.IsOvernight = dto.IsOvernight;
            st.IsWorkShift = dto.IsWorkShift;
            await db.SaveChangesAsync();
            return Results.Ok(st);
        }).RequireTeamEditor();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var st = await db.ShiftTypes.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId);
            if (st is null) return Results.NotFound();
            db.ShiftTypes.Remove(st);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }
}
