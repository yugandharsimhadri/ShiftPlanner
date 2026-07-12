using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class HolidaysEndpoints
{
    public static void MapHolidaysEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/holidays").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return await db.Holidays.Where(h => h.TeamId == teamId).OrderBy(h => h.Date).ToListAsync();
        }).RequireTeamMember();

        group.MapPost("", async (HolidayDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var holiday = new Holiday { TeamId = teamId, Date = dto.Date, Name = dto.Name };
            db.Holidays.Add(holiday);
            await db.SaveChangesAsync();
            return Results.Created($"/api/holidays/{holiday.Id}", holiday);
        }).RequireTeamEditor();

        group.MapPut("/{id:int}", async (int id, HolidayDto dto, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id && h.TeamId == teamId);
            if (holiday is null) return Results.NotFound();
            holiday.Date = dto.Date;
            holiday.Name = dto.Name;
            await db.SaveChangesAsync();
            return Results.Ok(holiday);
        }).RequireTeamEditor();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id && h.TeamId == teamId);
            if (holiday is null) return Results.NotFound();
            db.Holidays.Remove(holiday);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamEditor();
    }
}
