using System.IO;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.Data;

public class ShiftPlannerContext : DbContext
{
    public static string DbPath { get; } = Path.Combine(AppContext.BaseDirectory, "shiftplanner.db");

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Subtrack> Subtracks => Set<Subtrack>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<Holiday> Holidays => Set<Holiday>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShiftType>().HasKey(s => s.Code);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Track)
            .WithMany(t => t.Employees)
            .HasForeignKey(e => e.TrackId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Subtrack)
            .WithMany(s => s.Employees)
            .HasForeignKey(e => e.SubtrackId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Subtrack>()
            .HasOne(s => s.Track)
            .WithMany(t => t.Subtracks)
            .HasForeignKey(s => s.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RosterEntry>()
            .HasOne(r => r.Employee)
            .WithMany(e => e.RosterEntries)
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RosterEntry>()
            .HasOne(r => r.ShiftTypeRef)
            .WithMany()
            .HasForeignKey(r => r.ShiftCode)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RosterEntry>()
            .HasIndex(r => new { r.EmployeeId, r.Date })
            .IsUnique();
    }

    public void EnsureSeeded()
    {
        Database.EnsureCreated();

        if (Tracks.Any()) return;

        var frontDesk = new Track { Name = "Front Desk", Color = "#4453AD", LeadName = "Aisha Rahman" };
        var warehouse = new Track { Name = "Warehouse", Color = "#A8701F", LeadName = "Marcus Webb" };
        Tracks.AddRange(frontDesk, warehouse);
        SaveChanges();

        var reception = new Subtrack { TrackId = frontDesk.Id, Name = "Reception" };
        var billing = new Subtrack { TrackId = frontDesk.Id, Name = "Billing" };
        var inbound = new Subtrack { TrackId = warehouse.Id, Name = "Inbound" };
        var outbound = new Subtrack { TrackId = warehouse.Id, Name = "Outbound" };
        Subtracks.AddRange(reception, billing, inbound, outbound);
        SaveChanges();

        Employees.AddRange(
            new Employee { Id = "EMP-014", Name = "Aisha Rahman", Phone = "98450 12001", TrackId = frontDesk.Id, SubtrackId = reception.Id, Role = "Receptionist", WeeklyOff = DayOfWeek.Sunday },
            new Employee { Id = "EMP-015", Name = "Daniel Cho", Phone = "98450 12002", TrackId = frontDesk.Id, SubtrackId = billing.Id, Role = "Billing Clerk", WeeklyOff = DayOfWeek.Saturday },
            new Employee { Id = "EMP-022", Name = "Marcus Webb", Phone = "98450 12003", TrackId = warehouse.Id, SubtrackId = inbound.Id, Role = "Picker", WeeklyOff = DayOfWeek.Sunday },
            new Employee { Id = "EMP-023", Name = "Priya Nair", Phone = "98450 12004", TrackId = warehouse.Id, SubtrackId = outbound.Id, Role = "Picker", WeeklyOff = DayOfWeek.Wednesday },
            new Employee { Id = "EMP-024", Name = "Tom Alvarez", Phone = "98450 12005", TrackId = warehouse.Id, SubtrackId = outbound.Id, Role = "Loader", EmploymentType = EmploymentType.PartTime, WeeklyOff = DayOfWeek.Tuesday }
        );

        ShiftTypes.AddRange(
            new ShiftType { Code = "M", Name = "Morning", Start = new TimeOnly(6, 0), End = new TimeOnly(14, 0), Color = "#A8701F", SortOrder = 1 },
            new ShiftType { Code = "E", Name = "Evening", Start = new TimeOnly(14, 0), End = new TimeOnly(22, 0), Color = "#4453AD", SortOrder = 2 },
            new ShiftType { Code = "N", Name = "Night", Start = new TimeOnly(22, 0), End = new TimeOnly(6, 0), Color = "#22314F", IsOvernight = true, SortOrder = 3 },
            new ShiftType { Code = "OFF", Name = "Off", Color = "#6E7D78", SortOrder = 4 },
            new ShiftType { Code = "LV", Name = "Leave", Color = "#A5392B", SortOrder = 5 }
        );

        var today = DateTime.Today;
        Holidays.Add(new Holiday { Date = new DateOnly(today.Year, 8, 15), Name = "Independence Day" });
        SaveChanges();

        var pattern = new[] { "M", "M", "OFF", "M", "M", "E", "OFF" };
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var employeeIds = new[] { "EMP-014", "EMP-015", "EMP-022", "EMP-023", "EMP-024" };
        for (var d = monthStart; d.Month == monthStart.Month; d = d.AddDays(1))
        {
            var dayIndex = d.DayNumber % pattern.Length;
            foreach (var empId in employeeIds)
                RosterEntries.Add(new RosterEntry { EmployeeId = empId, Date = d, ShiftCode = pattern[dayIndex], Source = EntrySource.Manual });
        }
        SaveChanges();
    }
}
