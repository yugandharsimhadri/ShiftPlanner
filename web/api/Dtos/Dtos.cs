using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Dtos;

public record TrackDto(int? Id, string Name, string? LeadName, string Color);

public record SubtrackDto(int? Id, int TrackId, string Name);

public record LocationDto(int? Id, string Name);

public record JobRoleDto(int? Id, string Name);

public record ShiftTypeDto(int? Id, string Code, string Name, TimeOnly? Start, TimeOnly? End, string Color, bool IsOvernight, bool IsWorkShift);

public record HolidayDto(int? Id, DateOnly Date, string Name);

public record RosterEntryUpsertDto(int TeamMemberId, DateOnly Date, string? ShiftCode, string? Note);

public record CopyForwardRequest(
    int SourceYear,
    int SourceMonth,
    int TargetYear,
    int TargetMonth,
    string Pattern, // "weekday" | "exact-date"
    bool SkipInactive
);

public record CopyForwardFlag(int TeamMemberId, string MemberName, DateOnly Date, string Reason);

public record CopyForwardResult(int CopiedCount, List<CopyForwardFlag> Flagged);

public record ImportRowError(int Row, string Message);

public record ImportResult(int Imported, List<ImportRowError> Errors);

// --- Teams ---------------------------------------------------------------

public record CreateTeamDto(string Name);

public record TeamSummaryDto(int Id, string Name, TeamRole Role);

// --- Team members (merged Employee + TeamMembership) ------------------------

public record TeamMemberDto(
    int Id,
    Guid PersonId,
    string Name,
    string Phone,
    string? Email,
    bool HasLogin,
    string Code,
    int? TrackId,
    string? TrackName,
    int? SubtrackId,
    string? SubtrackName,
    int? JobRoleId,
    string? JobRoleName,
    int? LocationId,
    string? LocationName,
    EmploymentType EmploymentType,
    DateOnly JoinDate,
    EmployeeStatus Status,
    string? Notes,
    TeamRole AccessRole,
    bool IsTeamLead,
    bool IsCoLead,
    DateTimeOffset CreatedAt
);

// Creates a new Person plus one TeamMember row per team in TeamIds — TeamIds may be
// empty, leaving the person recorded but not yet assigned to any team's roster.
public record CreateTeamMemberDto(
    string Name,
    string Phone,
    string? Email,
    string? Notes,
    string Code,
    int? TrackId,
    int? SubtrackId,
    int? JobRoleId,
    int? LocationId,
    EmploymentType EmploymentType,
    DateOnly JoinDate,
    EmployeeStatus Status,
    TeamRole AccessRole,
    List<int> TeamIds
);

public record UpdateTeamMemberDto(
    string Name,
    string Phone,
    string? Email,
    string? Notes,
    string Code,
    int? TrackId,
    int? SubtrackId,
    int? JobRoleId,
    int? LocationId,
    EmploymentType EmploymentType,
    DateOnly JoinDate,
    EmployeeStatus Status,
    TeamRole AccessRole
);

// Adds an existing (already-known-to-you) Person to another team you manage.
public record AssignPersonToTeamDto(
    Guid PersonId,
    int TeamId,
    string Code,
    int? TrackId,
    int? SubtrackId,
    int? JobRoleId,
    int? LocationId,
    EmploymentType EmploymentType,
    DateOnly JoinDate,
    TeamRole AccessRole
);

public record UnassignedPersonDto(Guid Id, string Name, string Phone, string? Email);

public record UpdateMemberRoleDto(TeamRole AccessRole);

public record SetCoLeadDto(bool IsCoLead);

// The caller's own TeamMember record on the current team.
public record MeDto(Guid PersonId, string Name, string Code, TeamRole Role, bool IsTeamLead, bool IsCoLead);

public record TeamSettingsDto(
    string Name,
    string? OrgName,
    int? TeamStrength,
    string? ShiftsCovered,
    List<DayOfWeek> DefaultOffDays,
    bool CompOffsEnabled,
    int ActiveMemberCount,
    string? LeadName,
    string? CoLeadName
);

public record UpdateTeamSettingsDto(
    string Name,
    string? OrgName,
    int? TeamStrength,
    string? ShiftsCovered,
    List<DayOfWeek> DefaultOffDays,
    bool CompOffsEnabled
);

// --- Auth ------------------------------------------------------------------

// Either Email or Phone (or both) must be set — whichever is given can later be used
// to log in. Doesn't replace the built-in /api/register (still email+password only,
// still used by Mobile); this is the newer email-or-phone flow used by Web.
public record RegisterAccountDto(string? Email, string? Phone, string Password);

public record LoginPhoneDto(string Phone, string Password);

// --- Comp-offs ---------------------------------------------------------------

public record CompOffEntryDto(
    int Id,
    int TeamMemberId,
    string MemberCode,
    string MemberName,
    DateOnly EarnedDate,
    CompOffStatus Status,
    DateOnly? UsedDate
);

public record UseCompOffDto(DateOnly UsedDate);

// --- Live availability ---------------------------------------------------------
// Independent of the planned roster entirely — a member's self-reported "free right
// now" status, which auto-expires per their own configured window (see
// AvailabilityService). Visible to every team member, not just admins.

public record TeamMemberAvailabilityDto(
    int TeamMemberId,
    Guid PersonId,
    string Name,
    string Code,
    string? TrackName,
    bool IsAvailable,
    DateTimeOffset? AvailableSince,
    string? Timezone
);

public record UpdateAvailabilityDto(bool IsAvailable);

// --- Profile ---------------------------------------------------------------------
// Person-level (not team-scoped) — timezone and the availability auto-expiry window,
// both self-service. AutoExpiryHours is always the *effective* value (override if set,
// otherwise the timezone-based default); AutoExpiryHoursOverride is the raw stored value.

public record ProfileDto(
    string Name,
    string? Email,
    string Phone,
    string? Timezone,
    int AutoExpiryHours,
    int? AutoExpiryHoursOverride
);

public record UpdateProfileDto(string? Timezone, int? AutoExpiryHoursOverride);

// --- Managers ----------------------------------------------------------------------
// A Manager has read-only oversight of the live-availability dashboard across every
// team they're assigned to, without gaining roster-edit rights on teams that aren't
// theirs (see ManagerAssignment).

public record PersonSearchResultDto(Guid Id, string Name, string Phone, string? Email);

public record GrantManagerDto(Guid PersonId);

public record ManagerAssignmentDto(int Id, Guid PersonId, string PersonName, string PersonPhone, int TeamId, string TeamName);

public record ManagerTeamAvailabilityDto(int TeamId, string TeamName, List<TeamMemberAvailabilityDto> Members);

public record ManagerTeamDto(int Id, string Name);

// --- Reports -------------------------------------------------------------------

public record UtilizationRowDto(
    int TeamMemberId,
    string MemberCode,
    string MemberName,
    string? TrackName,
    int TotalShiftsWorked,
    int WeekendShiftsWorked,
    int CompOffsEarned,
    int CompOffsUsed,
    int CompOffsPending
);
