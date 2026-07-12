using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Subtrack> Subtracks => Set<Subtrack>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<Holiday> Holidays => Set<Holiday>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Team / membership -----------------------------------------------

        builder.Entity<TeamMembership>()
            .HasOne(m => m.Team)
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // One membership row per (team, email) — re-inviting the same email just
        // updates the existing row rather than duplicating it.
        builder.Entity<TeamMembership>()
            .HasIndex(m => new { m.TeamId, m.Email })
            .IsUnique();

        // --- Tenant-scoped tables ----------------------------------------------
        // Every one of these carries TeamId directly (not just via a join) so no
        // query can accidentally leak across teams by forgetting to traverse a
        // relationship — every endpoint filters on TeamId as a plain column.

        builder.Entity<Employee>()
            .HasOne(e => e.Track)
            .WithMany(t => t.Employees)
            .HasForeignKey(e => e.TrackId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Employee>()
            .HasOne(e => e.Subtrack)
            .WithMany()
            .HasForeignKey(e => e.SubtrackId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Employee>()
            .HasIndex(e => new { e.TeamId, e.Code })
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
            .HasOne(r => r.Employee)
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RosterEntry>()
            .HasIndex(r => new { r.EmployeeId, r.Date })
            .IsUnique();
    }
}
