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
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<CompOffEntry> CompOffEntries => Set<CompOffEntry>();

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
    }
}
