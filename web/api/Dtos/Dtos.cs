using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Dtos;

public record EmployeeDto(
    string Code,
    string Name,
    string Phone,
    string? Email,
    int TrackId,
    int? SubtrackId,
    string Role,
    EmploymentType EmploymentType,
    DateOnly JoinDate,
    DayOfWeek? WeeklyOff,
    EmployeeStatus Status,
    string? Notes
);

public record TrackDto(int? Id, string Name, string? LeadName, string Color);

public record SubtrackDto(int? Id, int TrackId, string Name);

public record ShiftTypeDto(int? Id, string Code, string Name, TimeOnly? Start, TimeOnly? End, string Color, bool IsOvernight);

public record HolidayDto(int? Id, DateOnly Date, string Name);

public record RosterEntryUpsertDto(Guid EmployeeId, DateOnly Date, string? ShiftCode, string? Note);

public record CopyForwardRequest(
    int SourceYear,
    int SourceMonth,
    int TargetYear,
    int TargetMonth,
    string Pattern, // "weekday" | "exact-date"
    bool SkipInactive
);

public record CopyForwardFlag(Guid EmployeeId, string EmployeeName, DateOnly Date, string Reason);

public record CopyForwardResult(int CopiedCount, List<CopyForwardFlag> Flagged);

public record ImportRowError(int Row, string Message);

public record ImportResult(int Imported, List<ImportRowError> Errors);

// --- Teams ---------------------------------------------------------------

public record CreateTeamDto(string Name);

public record TeamSummaryDto(int Id, string Name, TeamRole Role);

public record AddMemberDto(string Email, TeamRole Role);

public record UpdateMemberRoleDto(TeamRole Role);

public record MembershipDto(
    int Id,
    string Email,
    TeamRole Role,
    MembershipStatus Status,
    Guid? EmployeeId,
    DateTimeOffset CreatedAt
);

public record LinkEmployeeDto(Guid? EmployeeId);

public record MeDto(string Email, TeamRole Role, Guid? EmployeeId, string? EmployeeCode);
