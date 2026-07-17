using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Subtrack> Subtracks => Set<Subtrack>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<JobRole> JobRoles => Set<JobRole>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<CompOffEntry> CompOffEntries => Set<CompOffEntry>();
    public DbSet<ManagerAssignment> ManagerAssignments => Set<ManagerAssignment>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<ShiftSwapRequest> ShiftSwapRequests => Set<ShiftSwapRequest>();
    public DbSet<RosterEntryHistory> RosterEntryHistories => Set<RosterEntryHistory>();
    public DbSet<RosterMonthStatus> RosterMonthStatuses => Set<RosterMonthStatus>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Team members ------------------------------------------------------
        // TeamMember is both "who has access to this team" and "who is on this
        // team's roster" — one row per (team, person). Every one of these tenant-
        // scoped tables carries TeamId directly (not just via a join) so no query
        // can accidentally leak across teams by forgetting to traverse a relationship.

        builder.Entity<TeamMember>()
            .HasOne(m => m.Team)
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TeamMember>()
            .HasOne(m => m.Person)
            .WithMany()
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TeamMember>()
            .HasOne(m => m.Track)
            .WithMany(t => t.TeamMembers)
            .HasForeignKey(m => m.TrackId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TeamMember>()
            .HasOne(m => m.Subtrack)
            .WithMany()
            .HasForeignKey(m => m.SubtrackId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TeamMember>()
            .HasOne(m => m.JobRole)
            .WithMany()
            .HasForeignKey(m => m.JobRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TeamMember>()
            .HasOne(m => m.Location)
            .WithMany()
            .HasForeignKey(m => m.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TeamMember>()
            .HasIndex(m => new { m.TeamId, m.Code })
            .IsUnique();

        builder.Entity<Subtrack>()
            .HasOne(s => s.Track)
            .WithMany(t => t.Subtracks)
            .HasForeignKey(s => s.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ShiftType>()
            .HasIndex(s => new { s.TeamId, s.Code })
            .IsUnique();

        builder.Entity<RosterEntry>()
            .HasOne(r => r.TeamMember)
            .WithMany()
            .HasForeignKey(r => r.TeamMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RosterEntry>()
            .HasIndex(r => new { r.TeamMemberId, r.Date })
            .IsUnique();

        builder.Entity<CompOffEntry>()
            .HasOne(c => c.TeamMember)
            .WithMany()
            .HasForeignKey(c => c.TeamMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ManagerAssignment>()
            .HasOne(a => a.Person)
            .WithMany()
            .HasForeignKey(a => a.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ManagerAssignment>()
            .HasOne(a => a.Team)
            .WithMany()
            .HasForeignKey(a => a.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ManagerAssignment>()
            .HasIndex(a => new { a.PersonId, a.TeamId })
            .IsUnique();

        // --- Leave requests ------------------------------------------------------

        builder.Entity<LeaveRequest>()
            .HasOne(l => l.TeamMember)
            .WithMany()
            .HasForeignKey(l => l.TeamMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Shift swap requests --------------------------------------------------
        // Three separate FKs onto TeamMember (offered-by, target, claimed-by) — all
        // Restrict rather than Cascade, since a swap request shouldn't silently vanish
        // (or cascade-delete something else) just because one referenced member is
        // later removed from the team; the endpoint layer handles that case explicitly.

        builder.Entity<ShiftSwapRequest>()
            .HasOne(s => s.OfferedByTeamMember)
            .WithMany()
            .HasForeignKey(s => s.OfferedByTeamMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShiftSwapRequest>()
            .HasOne(s => s.TargetTeamMember)
            .WithMany()
            .HasForeignKey(s => s.TargetTeamMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShiftSwapRequest>()
            .HasOne(s => s.ClaimedByTeamMember)
            .WithMany()
            .HasForeignKey(s => s.ClaimedByTeamMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Roster entry history -------------------------------------------------

        builder.Entity<RosterEntryHistory>()
            .HasOne(h => h.TeamMember)
            .WithMany()
            .HasForeignKey(h => h.TeamMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RosterEntryHistory>()
            .HasIndex(h => new { h.TeamId, h.Date });

        // --- Roster month publish status ------------------------------------------

        builder.Entity<RosterMonthStatus>()
            .HasIndex(r => new { r.TeamId, r.Year, r.Month })
            .IsUnique();
    }
}
