using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

// Person-level settings — timezone and the availability auto-expiry window — not
// scoped to any one team, so these don't go through the X-Team-Id-gated filters.
// Requires a Person row to already exist, which is true for anyone who's actually
// inside the app (Person is created the moment you create or join your first team).
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/me").RequireAuthorization();

        group.MapGet("/profile", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await ClaimPendingAsync(db, userId);
            var person = await db.People.FirstOrDefaultAsync(p => p.UserId == userId);
            if (person is null) return Results.NotFound();

            return Results.Ok(ToDto(person));
        });

        group.MapPatch("/profile", async (UpdateProfileDto dto, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await ClaimPendingAsync(db, userId);
            var person = await db.People.FirstOrDefaultAsync(p => p.UserId == userId);
            if (person is null) return Results.NotFound();

            if (dto.Timezone is not null)
                person.Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? null : dto.Timezone.Trim();

            person.AvailabilityAutoExpiryHoursOverride = dto.AutoExpiryHoursOverride;

            await db.SaveChangesAsync();
            return Results.Ok(ToDto(person));
        });
    }

    private static ProfileDto ToDto(Models.Person p) => new(
        p.Name, p.Email, p.Phone ?? "", p.Timezone,
        AvailabilityService.EffectiveAutoExpiryHours(p), p.AvailabilityAutoExpiryHoursOverride);

    private static async Task ClaimPendingAsync(AppDbContext db, string userId)
    {
        var account = await db.Users.Where(u => u.Id == userId).Select(u => new { u.Email, u.PhoneNumber }).FirstOrDefaultAsync();
        if (account is not null)
            await PendingInviteClaimer.ClaimAsync(db, userId, account.Email, account.PhoneNumber);
    }
}
