using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;

namespace ShiftPlanner.Api.Endpoints;

// Login is optional for a team member, and when someone does want to log in, either an
// email or a phone number works — both are checked against the same IdentityUser record
// (see Person.cs / PendingInviteClaimer). The built-in MapIdentityApi endpoints already
// cover email+password (/api/register, /api/login) and Mobile still uses those unchanged;
// these two endpoints add the phone-number half without touching that.
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Creates an account from an email and/or a phone number (at least one required)
        // plus a password. Doesn't sign the account in — call /api/login or
        // /api/login-phone afterward, same as the built-in /api/register does for email.
        app.MapPost("/api/register-account", async (RegisterAccountDto dto, UserManager<IdentityUser> userManager, AppDbContext db) =>
        {
            var email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            var phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();

            if (email is null && phone is null)
                return Results.BadRequest(new { message = "Enter an email or phone number." });

            if (phone is not null && await db.Users.AnyAsync(u => u.PhoneNumber == phone))
                return Results.Conflict(new { message = "That phone number is already registered." });

            var user = new IdentityUser
            {
                UserName = email ?? phone,
                Email = email,
                PhoneNumber = phone,
                EmailConfirmed = email is not null,
                PhoneNumberConfirmed = phone is not null,
            };

            var result = await userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

            return Results.Ok();
        });

        // Same bearer-token issuance the built-in /api/login uses for email, just resolving
        // the account by phone number first.
        app.MapPost("/api/login-phone", async (LoginPhoneDto dto, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) =>
        {
            var user = await userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.Phone);
            if (user is null) return Results.Unauthorized();

            signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
            var result = await signInManager.PasswordSignInAsync(user, dto.Password, isPersistent: false, lockoutOnFailure: false);
            if (!result.Succeeded) return Results.Unauthorized();

            // PasswordSignInAsync already wrote the bearer-token response body.
            return Results.Empty;
        });
    }
}
