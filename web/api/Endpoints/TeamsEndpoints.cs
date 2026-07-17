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

        // Create a team. The caller becomes its Admin and Lead immediately — as a
        // TeamMember, same as everyone else, not a separate concept. Team names
        // aren't unique platform-wide, but the same person can't create two teams
        // with the same name.
        group.MapPost("", async (CreateTeamDto dto, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var account = await db.Users.Where(u => u.Id == userId).Select(u => new { u.Email, u.PhoneNumber }).FirstAsync();

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

            // Every team starts with the same Location/JobRole starter lists — admins can
            // add more from Settings or right from the team-member form.
            var jobRoles = MasterDataSeed.JobRoles.Select(n => new JobRole { TeamId = team.Id, Name = n }).ToList();
            db.Locations.AddRange(MasterDataSeed.Cities.Select(n => new Location { TeamId = team.Id, Name = n }));
            db.JobRoles.AddRange(jobRoles);
            await db.SaveChangesAsync();

            // Reuse an existing Person for this login if one's already linked (e.g. they
            // were added to another team earlier and it got claimed); otherwise create one.
            var person = await db.People.FirstOrDefaultAsync(p => p.UserId == userId);
            if (person is null)
            {
                person = new Person
                {
                    Name = account.Email ?? account.PhoneNumber ?? "Admin",
                    Phone = account.PhoneNumber,
                    Email = account.Email,
                    UserId = userId,
                    CreatedByUserId = userId,
                };
                db.People.Add(person);
                await db.SaveChangesAsync();
            }

            db.TeamMembers.Add(new TeamMember
            {
                TeamId = team.Id,
                PersonId = person.Id,
                Code = "EMP-001",
                JobRoleId = jobRoles.First(r => r.Name == "Admin").Id,
                EmploymentType = EmploymentType.FullTime,
                JoinDate = DateOnly.FromDateTime(DateTime.Today),
                AccessRole = TeamRole.Admin,
                IsTeamLead = true,
            });
            await db.SaveChangesAsync();

            return Results.Created($"/api/teams/{team.Id}", new TeamSummaryDto(team.Id, team.Name, TeamRole.Admin));
        });

        // List every team the caller belongs to, with their role in each — powers the
        // team switcher. Also claims any not-yet-linked People first, so a team an
        // admin just added this email/phone to shows up immediately.
        group.MapGet("/mine", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var account = await db.Users.Where(u => u.Id == userId).Select(u => new { u.Email, u.PhoneNumber }).FirstAsync();

            await PendingInviteClaimer.ClaimAsync(db, userId, account.Email, account.PhoneNumber);

            var teams = await db.TeamMembers
                .Where(m => m.Person!.UserId == userId)
                .Include(m => m.Team)
                .Select(m => new TeamSummaryDto(m.TeamId, m.Team!.Name, m.AccessRole))
                .ToListAsync();

            return Results.Ok(teams);
        });

        // --- Team members (scoped to the team named in X-Team-Id) --------------
        // One list, one entity — a TeamMember is both "has access" and "is on the
        // roster." Read access is open to any member; creating/editing/removing is
        // Admin-only, since adding someone here can also grant them Editor/Admin access.

        var members = app.MapGroup("/api/teams/current/members");

        members.MapGet("", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var list = await db.TeamMembers
                .Where(m => m.TeamId == ctx.TeamId)
                .Include(m => m.Person)
                .Include(m => m.Track)
                .Include(m => m.Subtrack)
                .Include(m => m.JobRole)
                .Include(m => m.Location)
                .OrderBy(m => m.Person!.Name)
                .ToListAsync();

            return Results.Ok(list.Select(ToDto));
        }).RequireTeamMember();

        // The caller's own team-member record on the current team.
        members.MapGet("/me", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers
                .Include(m => m.Person)
                .FirstOrDefaultAsync(m => m.TeamId == ctx.TeamId && m.Person!.UserId == ctx.UserId);

            if (member is null) return Results.NotFound();
            return Results.Ok(new MeDto(member.PersonId, member.Person!.Name, member.Code, member.AccessRole, member.IsTeamLead, member.IsCoLead));
        }).RequireTeamMember();

        // A suggested next code (e.g. "EMP-004") for the add-member form to pre-fill —
        // still editable, since Code is per-team and user-chosen.
        members.MapGet("/next-code", async (AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            return Results.Ok(new { code = await ImportEndpoints.SuggestNextTeamMemberCode(db, teamId) });
        }).RequireTeamMember();

        // People this admin already manages (on any of their teams) who aren't yet
        // assigned to THIS team — candidates for "add to this team too."
        members.MapGet("/unassigned-candidates", async (AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var alreadyHere = db.TeamMembers.Where(m => m.TeamId == ctx.TeamId).Select(m => m.PersonId);

            var candidates = await db.People
                .Where(p => p.CreatedByUserId == ctx.UserId && !alreadyHere.Contains(p.Id))
                .Select(p => new UnassignedPersonDto(p.Id, p.Name, p.Phone ?? "", p.Email))
                .ToListAsync();

            return Results.Ok(candidates);
        }).RequireTeamAdmin();

        // Creates a new person plus one TeamMember row per requested team (TeamIds may
        // be empty, leaving them recorded but unassigned anywhere). The caller must be
        // Admin on every requested team, not just the current one.
        members.MapPost("", async (CreateTeamMemberDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var name = dto.Name.Trim();
            var phone = dto.Phone.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Name is required." });

            var teamIds = (dto.TeamIds ?? new List<int>()).Distinct().ToList();
            foreach (var teamId in teamIds)
            {
                var isAdminThere = await db.TeamMembers.AnyAsync(m =>
                    m.TeamId == teamId && m.Person!.UserId == ctx.UserId && m.AccessRole == TeamRole.Admin);
                if (!isAdminThere)
                    return Results.Forbid();
            }

            var code = dto.Code.Trim();
            if (teamIds.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(code))
                    return Results.BadRequest(new { message = "Code is required when assigning to a team." });

                foreach (var teamId in teamIds)
                {
                    if (await db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.Code == code))
                        return Results.Conflict(new { message = $"Code '{code}' is already in use on one of the selected teams." });
                }
            }

            var email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            var person = new Person
            {
                Name = name,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Email = email,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                CreatedByUserId = ctx.UserId,
            };

            // If this phone/email already belongs to a signed-in account, link immediately
            // instead of waiting for them to log in.
            var matchingUser = await db.Users.FirstOrDefaultAsync(u =>
                (email != null && u.Email == email) || (person.Phone != null && u.PhoneNumber == person.Phone));
            if (matchingUser is not null) person.UserId = matchingUser.Id;

            db.People.Add(person);
            await db.SaveChangesAsync();

            TeamMember? primary = null;
            foreach (var teamId in teamIds)
            {
                var member = new TeamMember
                {
                    TeamId = teamId,
                    PersonId = person.Id,
                    Code = code,
                    TrackId = dto.TrackId,
                    SubtrackId = dto.SubtrackId,
                    JobRoleId = dto.JobRoleId,
                    LocationId = dto.LocationId,
                    EmploymentType = dto.EmploymentType,
                    JoinDate = dto.JoinDate,
                    Status = dto.Status,
                    Notes = person.Notes,
                    AccessRole = dto.AccessRole,
                };
                db.TeamMembers.Add(member);
                if (teamId == ctx.TeamId) primary = member;
                primary ??= member;
            }
            await db.SaveChangesAsync();

            if (primary is null)
                return Results.Created($"/api/teams/current/members/unassigned/{person.Id}",
                    new UnassignedPersonDto(person.Id, person.Name, person.Phone ?? "", person.Email));

            return Results.Created($"/api/teams/current/members/{primary.Id}", await BuildDto(db, primary, person));
        }).RequireTeamAdmin();

        // Adds an already-known person (one you manage elsewhere) to the current team.
        members.MapPost("/assign-existing", async (AssignPersonToTeamDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            if (dto.TeamId != ctx.TeamId)
                return Results.BadRequest(new { message = "Team mismatch." });

            var person = await db.People.FirstOrDefaultAsync(p => p.Id == dto.PersonId && p.CreatedByUserId == ctx.UserId);
            if (person is null) return Results.NotFound();

            var code = dto.Code.Trim();
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { message = "Code is required." });
            if (await db.TeamMembers.AnyAsync(m => m.TeamId == ctx.TeamId && m.Code == code))
                return Results.Conflict(new { message = $"Code '{code}' is already in use on this team." });
            if (await db.TeamMembers.AnyAsync(m => m.TeamId == ctx.TeamId && m.PersonId == person.Id))
                return Results.Conflict(new { message = "This person is already on this team." });

            var member = new TeamMember
            {
                TeamId = ctx.TeamId,
                PersonId = person.Id,
                Code = code,
                TrackId = dto.TrackId,
                SubtrackId = dto.SubtrackId,
                JobRoleId = dto.JobRoleId,
                LocationId = dto.LocationId,
                EmploymentType = dto.EmploymentType,
                JoinDate = dto.JoinDate,
                AccessRole = dto.AccessRole,
            };
            db.TeamMembers.Add(member);
            await db.SaveChangesAsync();

            return Results.Created($"/api/teams/current/members/{member.Id}", await BuildDto(db, member, person));
        }).RequireTeamAdmin();

        // Full edit — including the shared Person fields (name/phone/email/notes), since
        // those genuinely belong to the person, not just this one team's row.
        members.MapPut("/{memberId:int}", async (int memberId, UpdateTeamMemberDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers.Include(m => m.Person)
                .FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == ctx.TeamId);
            if (member is null) return Results.NotFound();

            var code = dto.Code.Trim();
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { message = "Code is required." });
            if (code != member.Code && await db.TeamMembers.AnyAsync(m => m.TeamId == ctx.TeamId && m.Code == code && m.Id != memberId))
                return Results.Conflict(new { message = $"Code '{code}' is already in use on this team." });

            member.Person!.Name = dto.Name.Trim();
            member.Person.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            member.Person.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            member.Person.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            member.Code = code;
            member.TrackId = dto.TrackId;
            member.SubtrackId = dto.SubtrackId;
            member.JobRoleId = dto.JobRoleId;
            member.LocationId = dto.LocationId;
            member.EmploymentType = dto.EmploymentType;
            member.JoinDate = dto.JoinDate;
            member.Status = dto.Status;
            member.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            if (member.AccessRole == TeamRole.Admin && dto.AccessRole != TeamRole.Admin)
            {
                var otherAdmins = await db.TeamMembers.CountAsync(m =>
                    m.TeamId == ctx.TeamId && m.AccessRole == TeamRole.Admin && m.Id != memberId);
                if (otherAdmins == 0)
                    return Results.BadRequest(new { message = "A team needs at least one Admin — promote someone else first." });
                if (member.IsTeamLead)
                    return Results.BadRequest(new { message = "Transfer the lead to someone else before demoting this member." });
                member.IsCoLead = false;
            }
            member.AccessRole = dto.AccessRole;

            await db.SaveChangesAsync();
            return Results.Ok(await BuildDto(db, member, member.Person));
        }).RequireTeamAdmin();

        // Quick access-role change (the inline role dropdown) without touching anything else.
        members.MapPatch("/{memberId:int}/role", async (int memberId, UpdateMemberRoleDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers.Include(m => m.Person).FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == ctx.TeamId);
            if (member is null) return Results.NotFound();

            if (member.AccessRole == TeamRole.Admin && dto.AccessRole != TeamRole.Admin)
            {
                var otherAdmins = await db.TeamMembers.CountAsync(m =>
                    m.TeamId == ctx.TeamId && m.AccessRole == TeamRole.Admin && m.Id != memberId);
                if (otherAdmins == 0)
                    return Results.BadRequest(new { message = "A team needs at least one Admin — promote someone else first." });
                if (member.IsTeamLead)
                    return Results.BadRequest(new { message = "Transfer the lead to someone else before demoting this member." });
                member.IsCoLead = false;
            }

            member.AccessRole = dto.AccessRole;
            await db.SaveChangesAsync();
            return Results.Ok(await BuildDto(db, member, member.Person!));
        }).RequireTeamAdmin();

        // Transfers the "Lead" label to another team member — a Team Settings
        // configuration action, so any Admin can call it (not just whoever currently
        // holds the label). At most one lead per team, enforced by unsetting whoever
        // had it before.
        members.MapPatch("/{memberId:int}/lead", async (int memberId, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();

            var target = await db.TeamMembers.Include(m => m.Person).FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == ctx.TeamId);
            if (target is null) return Results.NotFound();
            if (target.Status != EmployeeStatus.Active)
                return Results.BadRequest(new { message = "Only an active member can become the lead." });

            var previousLead = await db.TeamMembers
                .Where(m => m.TeamId == ctx.TeamId && m.IsTeamLead && m.Id != memberId)
                .ToListAsync();
            foreach (var m in previousLead) m.IsTeamLead = false;

            target.AccessRole = TeamRole.Admin;
            target.IsTeamLead = true;
            target.IsCoLead = false;

            await db.SaveChangesAsync();
            return Results.Ok(await BuildDto(db, target, target.Person!));
        }).RequireTeamAdmin();

        // Sets or clears the "Co-Lead" label — at most one at a time. Same as above,
        // a Team Settings configuration action open to any Admin.
        members.MapPatch("/{memberId:int}/co-lead", async (int memberId, SetCoLeadDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();

            var target = await db.TeamMembers.Include(m => m.Person).FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == ctx.TeamId);
            if (target is null) return Results.NotFound();

            if (dto.IsCoLead)
            {
                if (target.IsTeamLead)
                    return Results.BadRequest(new { message = "The lead can't also be the co-lead." });
                if (target.Status != EmployeeStatus.Active)
                    return Results.BadRequest(new { message = "Only an active member can become co-lead." });

                var previousCoLead = await db.TeamMembers
                    .Where(m => m.TeamId == ctx.TeamId && m.IsCoLead && m.Id != memberId)
                    .ToListAsync();
                foreach (var m in previousCoLead) m.IsCoLead = false;

                target.AccessRole = TeamRole.Admin;
                target.IsCoLead = true;
            }
            else
            {
                target.IsCoLead = false;
            }

            await db.SaveChangesAsync();
            return Results.Ok(await BuildDto(db, target, target.Person!));
        }).RequireTeamAdmin();

        // Removes this person from THIS team only — the Person record (and any other
        // team's TeamMember row for them) is untouched.
        members.MapDelete("/{memberId:int}", async (int memberId, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == ctx.TeamId);
            if (member is null) return Results.NotFound();

            if (member.AccessRole == TeamRole.Admin)
            {
                var otherAdmins = await db.TeamMembers.CountAsync(m =>
                    m.TeamId == ctx.TeamId && m.AccessRole == TeamRole.Admin && m.Id != memberId);
                if (otherAdmins == 0)
                    return Results.BadRequest(new { message = "A team needs at least one Admin — promote someone else before removing this one." });
            }

            if (member.IsTeamLead)
                return Results.BadRequest(new { message = "Transfer the lead to someone else before removing this member." });

            db.TeamMembers.Remove(member);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireTeamAdmin();

        // --- Team settings -------------------------------------------------------

        var settings = app.MapGroup("/api/teams/current/settings");

        settings.MapGet("", async (AppDbContext db, HttpContext http) => Results.Ok(await BuildSettingsDto(db, http.GetTeamContext().TeamId)))
            .RequireTeamMember();

        settings.MapPut("", async (UpdateTeamSettingsDto dto, AppDbContext db, HttpContext http) =>
        {
            var ctx = http.GetTeamContext();
            var team = await db.Teams.FirstAsync(t => t.Id == ctx.TeamId);

            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { message = "Team name is required." });

            if (!string.Equals(name, team.Name, StringComparison.OrdinalIgnoreCase))
            {
                var nameTaken = await db.Teams.AnyAsync(t =>
                    t.Id != team.Id && t.CreatedByUserId == team.CreatedByUserId && t.Name.ToLower() == name.ToLower());
                if (nameTaken)
                    return Results.Conflict(new { message = $"You already have a team named '{name}'. Pick a different name." });
            }
            team.Name = name;

            team.OrgName = string.IsNullOrWhiteSpace(dto.OrgName) ? null : dto.OrgName.Trim();
            team.TeamStrength = dto.TeamStrength;
            team.ShiftsCovered = string.IsNullOrWhiteSpace(dto.ShiftsCovered) ? null : dto.ShiftsCovered.Trim();
            team.DefaultOffDays = dto.DefaultOffDays ?? new List<DayOfWeek>();
            team.CompOffsEnabled = dto.CompOffsEnabled;
            team.AutoApproveLeaveRequests = dto.AutoApproveLeaveRequests;
            team.AutoApproveShiftSwaps = dto.AutoApproveShiftSwaps;

            await db.SaveChangesAsync();
            return Results.Ok(await BuildSettingsDto(db, ctx.TeamId));
        }).RequireTeamAdmin();
    }

    private static TeamMemberDto ToDto(TeamMember m) => new(
        m.Id, m.PersonId, m.Person!.Name, m.Person.Phone ?? "", m.Person.Email, m.Person.UserId != null,
        m.Code, m.TrackId, m.Track?.Name, m.SubtrackId, m.Subtrack?.Name,
        m.JobRoleId, m.JobRole?.Name, m.LocationId, m.Location?.Name,
        m.EmploymentType, m.JoinDate, m.Status, m.Notes, m.AccessRole, m.IsTeamLead, m.IsCoLead, m.CreatedAt);

    private static async Task<TeamMemberDto> BuildDto(AppDbContext db, TeamMember m, Person p)
    {
        string? trackName = m.TrackId is { } tid ? await db.Tracks.Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefaultAsync() : null;
        string? subtrackName = m.SubtrackId is { } sid ? await db.Subtracks.Where(s => s.Id == sid).Select(s => s.Name).FirstOrDefaultAsync() : null;
        string? jobRoleName = m.JobRoleId is { } rid ? await db.JobRoles.Where(r => r.Id == rid).Select(r => r.Name).FirstOrDefaultAsync() : null;
        string? locationName = m.LocationId is { } lid ? await db.Locations.Where(l => l.Id == lid).Select(l => l.Name).FirstOrDefaultAsync() : null;
        return new TeamMemberDto(
            m.Id, p.Id, p.Name, p.Phone ?? "", p.Email, p.UserId != null,
            m.Code, m.TrackId, trackName, m.SubtrackId, subtrackName,
            m.JobRoleId, jobRoleName, m.LocationId, locationName,
            m.EmploymentType, m.JoinDate, m.Status, m.Notes, m.AccessRole, m.IsTeamLead, m.IsCoLead, m.CreatedAt);
    }

    private static async Task<TeamSettingsDto> BuildSettingsDto(AppDbContext db, int teamId)
    {
        var team = await db.Teams.FirstAsync(t => t.Id == teamId);
        var activeMemberCount = await db.TeamMembers.CountAsync(m => m.TeamId == teamId && m.Status == EmployeeStatus.Active);

        return new TeamSettingsDto(
            team.Name, team.OrgName, team.TeamStrength, team.ShiftsCovered,
            team.DefaultOffDays, team.CompOffsEnabled, activeMemberCount,
            team.AutoApproveLeaveRequests, team.AutoApproveShiftSwaps);
    }

}
