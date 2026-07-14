using Microsoft.AspNetCore.Identity;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Data;

public static class DbSeeder
{
    public const string AdminEmail = "admin@shiftplanner.local";
    public const string AdminPassword = "ShiftAdmin!2026";
    public const string DemoTeamName = "Demo Team";

    public static async Task SeedAsync(AppDbContext db, UserManager<IdentityUser> userManager, ILogger logger)
    {
        db.Database.EnsureCreated();

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, AdminPassword);
            if (result.Succeeded)
            {
                logger.LogInformation("=====================================================");
                logger.LogInformation("Seeded admin user -> Email: {Email}  Password: {Password}", AdminEmail, AdminPassword);
                logger.LogInformation("=====================================================");
            }
            else
            {
                logger.LogError("Failed to seed admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        if (db.Teams.Any()) return;

        var team = new Team
        {
            Name = DemoTeamName,
            CreatedByUserId = admin.Id,
            OrgName = "Demo Organization",
            TeamStrength = 12,
            ShiftsCovered = "24x7",
            CompOffsEnabled = true,
        };
        db.Teams.Add(team);
        db.SaveChanges();

        var adminPerson = new Person
        {
            Name = "Admin",
            Phone = "0000000000",
            Email = AdminEmail,
            UserId = admin.Id,
            CreatedByUserId = admin.Id,
        };
        db.People.Add(adminPerson);

        var locations = MasterDataSeed.Cities.Select(name => new Location { TeamId = team.Id, Name = name }).ToList();
        var jobRoles = MasterDataSeed.JobRoles.Select(name => new JobRole { TeamId = team.Id, Name = name }).ToList();
        db.Locations.AddRange(locations);
        db.JobRoles.AddRange(jobRoles);
        db.SaveChanges();

        db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            PersonId = adminPerson.Id,
            Code = "EMP-001",
            JobRoleId = jobRoles.First(r => r.Name == "Admin").Id,
            EmploymentType = EmploymentType.FullTime,
            JoinDate = DateOnly.FromDateTime(DateTime.Today),
            AccessRole = TeamRole.Admin,
            IsTeamLead = true,
        });

        var frontDesk = new Track { TeamId = team.Id, Name = "Front Desk", LeadName = "Priya Nair", Color = "#4453AD" };
        var warehouse = new Track { TeamId = team.Id, Name = "Warehouse", LeadName = "Arjun Mehta", Color = "#A8701F" };
        db.Tracks.AddRange(frontDesk, warehouse);
        db.SaveChanges();

        db.Subtracks.AddRange(
            new Subtrack { TeamId = team.Id, TrackId = frontDesk.Id, Name = "Reception" },
            new Subtrack { TeamId = team.Id, TrackId = frontDesk.Id, Name = "Billing" },
            new Subtrack { TeamId = team.Id, TrackId = warehouse.Id, Name = "Inbound" },
            new Subtrack { TeamId = team.Id, TrackId = warehouse.Id, Name = "Outbound" }
        );

        db.ShiftTypes.AddRange(
            new ShiftType { TeamId = team.Id, Code = "M", Name = "Morning", Start = new TimeOnly(6, 0), End = new TimeOnly(14, 0), Color = "#A8701F", IsOvernight = false, IsWorkShift = true },
            new ShiftType { TeamId = team.Id, Code = "E", Name = "Evening", Start = new TimeOnly(14, 0), End = new TimeOnly(22, 0), Color = "#4453AD", IsOvernight = false, IsWorkShift = true },
            new ShiftType { TeamId = team.Id, Code = "N", Name = "Night", Start = new TimeOnly(22, 0), End = new TimeOnly(6, 0), Color = "#22314F", IsOvernight = true, IsWorkShift = true },
            new ShiftType { TeamId = team.Id, Code = "OFF", Name = "Off", Start = null, End = null, Color = "#6E7D78", IsOvernight = false, IsWorkShift = false },
            new ShiftType { TeamId = team.Id, Code = "LV", Name = "Leave", Start = null, End = null, Color = "#A5392B", IsOvernight = false, IsWorkShift = false },
            new ShiftType { TeamId = team.Id, Code = "CO", Name = "Comp Off", Start = null, End = null, Color = "#2F7D6B", IsOvernight = false, IsWorkShift = false }
        );

        var currentYear = DateTime.Now.Year;
        db.Holidays.Add(new Holiday { TeamId = team.Id, Date = new DateOnly(currentYear, 8, 15), Name = "Independence Day" });

        db.SaveChanges();
        logger.LogInformation("Seeded '{Team}' with tracks, subtracks, shift types, and holiday. Admin is a member.", DemoTeamName);
    }
}
