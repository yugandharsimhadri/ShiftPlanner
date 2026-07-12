using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class TeamsEndpoints
{
    public static void MapTeamsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teams").RequireAuthorization();

        // Create a team. The caller becomes its Admin and Lead immediately. Team names
        // aren't unique platform-wide (two different people can both have a "Warehouse
        // Team"), but the same person can't create two teams with the same name.
        group.MapPost("", async (CreateTeamDto dto, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstAsync();

            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Team name is required." });

            var alreadyOwnsThisName = await db.Teams
                .AnyAsync(t => t.CreatedByUserId == userId && t.Name.ToLower() == name.ToLower());
            if (alreadyOwnsThisName)
                return Results.Conflict(new { message = $"You already have a team named '{name}'. Pick a different name." });

            var team = new Team { Name = name, CreatedByUserId = userId };
            db.Teams.Add(team);
            await db.SaveChangesAsync();

            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = team.Id,
                UserId = userId,
                Email = email!,
                Role = TeamRole.Admin,
                Status = MembershipStatus.Active,
                IsTeamLead = true,
            });
            await db.SaveChangesAsync();

            return Results.Created($"/api/teams/{team.Id}", new TeamSummaryDto(team.Id, team.Name, TeamRole.Admin));
        });

        // List every team the caller belongs to, with their role in each — this is
        // what powers the team switcher. Also claims any pending invites first, so
        // a team an admin just added this email to shows up immediately.
        group.MapGet("/mine", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstAsync();

            await PendingInviteClaimer.ClaimAsync(db, userId, email!);

            var teams = await db.TeamMemberships
                .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
                .Include(m => m.Team)
                .Select(m => new TeamSummaryDto(m.TeamId, m.Team!.Name, m.Role))
                .ToListAsync();

            return Results.Ok(teams);
        });

        // --- Member management (scoped to the team named in X-Team-Id) ---------

        var members = app.MapGroup("/api/teams/current/members");

        members.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var list = await db.TeamMemberships
                .Where(m => m.TeamId == ctx.TeamId)
                .OrderBy(m => m.Email)
                .Select(m => new MembershipDto(m.Id, m.Email, m.Role, m.Status, m.EmployeeId, m.CreatedAt, m.IsTeamLead, m.IsCoLead))
                .ToListAsync();
            return Results.Ok(list);
        }).RequireTeamMember();

        // The caller's own membership on the current team — role, and the employee record
        // an admin has linked them to (if any). Mobile uses this to resolve "my shifts"
        // automatically instead of asking the person to type in their own employee code.
        members.MapGet("/me", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var membership = await db.TeamMemberships
                .Where(m => m.TeamId == ctx.TeamId && m.UserId == ctx.UserId)
                .Select(m => new { m.Role, m.EmployeeId, m.IsTeamLead, m.IsCoLead })
                .FirstOrDefaultAsync();

            if (membership is null) return Results.NotFound();

            string? employeeCode = null;
            if (membership.EmployeeId is { } employeeId)
            {
                employeeCode = await db.Employees
                    .Where(e => e.Id == employeeId && e.TeamId == ctx.TeamId)
                    .Select(e => e.Code)
                    .FirstOrDefaultAsync();
            }

            return Results.Ok(new MeDto(ctx.Email, membership.Role, membership.EmployeeId, employeeCode, membership.IsTeamLead, membership.IsCoLead));
        }).RequireTeamMember();

        members.MapPost("", async (AddMemberDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var email = dto.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return Results.BadRequest(new { message = "Email is required." });

            var existing = await db.TeamMemberships
                .FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Email == email);

            if (existing is not null)
            {
                // Re-adding an existing member just updates their role.
                existing.Role = dto.Role;
                await db.SaveChangesAsync();
                return Results.Ok(new MembershipDto(existing.Id, existing.Email, existing.Role, existing.Status, existing.EmployeeId, existing.CreatedAt, existing.IsTeamLead, existing.IsCoLead));
            }

            var matchingUserId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstOrDefaultAsync();

            var membership = new TeamMembership
            {
                TeamId = ctx.TeamId,
                Email = email,
                Role = dto.Role,
                UserId = matchingUserId,
                Status = matchingUserId is null ? MembershipStatus.Invited : MembershipStatus.Active
            };
            db.TeamMemberships.Add(membership);
            await db.SaveChangesAsync();

            return Results.Created($"/api/teams/current/members/{membership.Id}",
                new MembershipDto(membership.Id, membership.Email, membership.Role, membership.Status, membership.EmployeeId, membership.CreatedAt, membership.IsTeamLead, membership.IsCoLead));
        }).RequireTeamAdmin();

        members.MapPatch("/{membershipId:int}", async (int membershipId, UpdateMemberRoleDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var membership = await db.TeamMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TeamId == ctx.TeamId);
            if (membership is null) return Results.NotFound();

            if (membership.Role == TeamRole.Admin && dto.Role != TeamRole.Admin)
            {
                var otherAdmins = await db.TeamMemberships.CountAsync(m =>
                    m.TeamId == ctx.TeamId && m.Role == TeamRole.Admin && m.Id != membershipId);
                if (otherAdmins == 0)
                    return Results.BadRequest(new { message = "A team needs at least one Admin — promote someone else first." });

                if (membership.IsTeamLead)
                    return Results.BadRequest(new { message = "Transfer the lead to someone else before demoting this membership." });

                membership.IsCoLead = false;
            }

            membership.Role = dto.Role;
            await db.SaveChangesAsync();
            return Results.Ok(new MembershipDto(membership.Id, membership.Email, membership.Role, membership.Status, membership.EmployeeId, membership.CreatedAt, membership.IsTeamLead, membership.IsCoLead));
        }).RequireTeamAdmin();

        // Transfers the "Lead" label to another Admin on this team. Restricted to whoever
        // currently holds it — Lead/Co-Lead are labels on top of Admin, not a separate
        // permission tier, but only the Lead should be able to hand the title off.
        members.MapPatch("/{membershipId:int}/lead", async (int membershipId, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var caller = await db.TeamMemberships.FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.UserId == ctx.UserId);
            if (caller is null || !caller.IsTeamLead)
                return Results.BadRequest(new { message = "Only the current team lead can transfer the lead." });

            var target = await db.TeamMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TeamId == ctx.TeamId);
            if (target is null) return Results.NotFound();
            if (target.Status != MembershipStatus.Active)
                return Results.BadRequest(new { message = "Only an active member can become the lead." });

            target.Role = TeamRole.Admin;
            target.IsTeamLead = true;
            target.IsCoLead = false;
            caller.IsTeamLead = false;

            await db.SaveChangesAsync();
            return Results.Ok(new MembershipDto(target.Id, target.Email, target.Role, target.Status, target.EmployeeId, target.CreatedAt, target.IsTeamLead, target.IsCoLead));
        }).RequireTeamAdmin();

        // Sets or clears the "Co-Lead" label — at most one at a time. Restricted to the
        // current Lead, same reasoning as transferring the lead itself.
        members.MapPatch("/{membershipId:int}/co-lead", async (int membershipId, SetCoLeadDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var caller = await db.TeamMemberships.FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.UserId == ctx.UserId);
            if (caller is null || !caller.IsTeamLead)
                return Results.BadRequest(new { message = "Only the current team lead can assign a co-lead." });

            var target = await db.TeamMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TeamId == ctx.TeamId);
            if (target is null) return Results.NotFound();

            if (dto.IsCoLead)
            {
                if (target.IsTeamLead)
                    return Results.BadRequest(new { message = "The lead can't also be the co-lead." });
                if (target.Status != MembershipStatus.Active)
                    return Results.BadRequest(new { message = "Only an active member can become co-lead." });

                var previousCoLead = await db.TeamMemberships
                    .Where(m => m.TeamId == ctx.TeamId && m.IsCoLead && m.Id != membershipId)
                    .ToListAsync();
                foreach (var m in previousCoLead) m.IsCoLead = false;

                target.Role = TeamRole.Admin;
                target.IsCoLead = true;
            }
            else
            {
                target.IsCoLead = false;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new MembershipDto(target.Id, target.Email, target.Role, target.Status, target.EmployeeId, target.CreatedAt, target.IsTeamLead, target.IsCoLead));
        }).RequireTeamAdmin();

        // Links (or unlinks, with employeeId: null) a membership to a roster Employee record,
        // e.g. "this login is Priya Nair." Purely a convenience for Mobile's My Shifts —
        // nothing else in the API depends on it.
        members.MapPatch("/{membershipId:int}/employee", async (int membershipId, LinkEmployeeDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var membership = await db.TeamMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TeamId == ctx.TeamId);
            if (membership is null) return Results.NotFound();

            if (dto.EmployeeId is { } employeeId)
            {
                var employeeExists = await db.Employees.AnyAsync(e => e.Id == employeeId && e.TeamId == ctx.TeamId);
                if (!employeeExists)
                    return Results.BadRequest(new { message = "That employee wasn't found on this team." });
            }

            membership.EmployeeId = dto.EmployeeId;
            await db.SaveChangesAsync();
            return Results.Ok(new MembershipDto(membership.Id, membership.Email, membership.Role, membership.Status, membership.EmployeeId, membership.CreatedAt, membership.IsTeamLead, membership.IsCoLead));
        }).RequireTeamAdmin();

        members.MapDelete("/{membershipId:int}", async (int membershipId, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var membership = await db.TeamMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TeamId == ctx.TeamId);
            if (membership is null) return Results.NotFound();

            if (membership.Role == TeamRole.Admin)
            {
                var otherAdmins = await db.TeamMemberships.CountAsync(m =>
                    m.TeamId == ctx.TeamId && m.Role == TeamRole.Admin && m.Id != membershipId);
                if (otherAdmins == 0)
                    return Results.BadRequest(new { message = "A team needs at least one Admin — promote someone else before removing this one." });
            }

            if (membership.IsTeamLead)
                return Results.BadRequest(new { message = "Transfer the lead to someone else before removing this member." });

            db.TeamMemberships.Remove(membership);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamAdmin();

        // --- Team settings -------------------------------------------------------

        var settings = app.MapGroup("/api/teams/current/settings");

        settings.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var team = await db.Teams.FirstAsync(t => t.Id == ctx.TeamId);

            var activeEmployeeCount = await db.Employees.CountAsync(e => e.TeamId == ctx.TeamId && e.Status == EmployeeStatus.Active);
            var leadEmail = await db.TeamMemberships.Where(m => m.TeamId == ctx.TeamId && m.IsTeamLead).Select(m => m.Email).FirstOrDefaultAsync();
            var coLeadEmail = await db.TeamMemberships.Where(m => m.TeamId == ctx.TeamId && m.IsCoLead).Select(m => m.Email).FirstOrDefaultAsync();

            return Results.Ok(new TeamSettingsDto(
                team.Name, team.OrgName, team.TeamStrength, team.ShiftsCovered,
                team.DefaultOffDays, team.CompOffsEnabled, activeEmployeeCount, leadEmail, coLeadEmail));
        }).RequireTeamMember();

        settings.MapPut("", async (UpdateTeamSettingsDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var team = await db.Teams.FirstAsync(t => t.Id == ctx.TeamId);

            team.OrgName = string.IsNullOrWhiteSpace(dto.OrgName) ? null : dto.OrgName.Trim();
            team.TeamStrength = dto.TeamStrength;
            team.ShiftsCovered = string.IsNullOrWhiteSpace(dto.ShiftsCovered) ? null : dto.ShiftsCovered.Trim();
            team.DefaultOffDays = dto.DefaultOffDays ?? new List<DayOfWeek>();
            team.CompOffsEnabled = dto.CompOffsEnabled;

            await db.SaveChangesAsync();

            var activeEmployeeCount = await db.Employees.CountAsync(e => e.TeamId == ctx.TeamId && e.Status == EmployeeStatus.Active);
            var leadEmail = await db.TeamMemberships.Where(m => m.TeamId == ctx.TeamId && m.IsTeamLead).Select(m => m.Email).FirstOrDefaultAsync();
            var coLeadEmail = await db.TeamMemberships.Where(m => m.TeamId == ctx.TeamId && m.IsCoLead).Select(m => m.Email).FirstOrDefaultAsync();

            return Results.Ok(new TeamSettingsDto(
                team.Name, team.OrgName, team.TeamStrength, team.ShiftsCovered,
                team.DefaultOffDays, team.CompOffsEnabled, activeEmployeeCount, leadEmail, coLeadEmail));
        }).RequireTeamAdmin();
    }
}
