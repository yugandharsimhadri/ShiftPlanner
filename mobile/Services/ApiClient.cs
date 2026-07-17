using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShiftPlanner.Mobile.Models;
using Location = ShiftPlanner.Mobile.Models.Location;

namespace ShiftPlanner.Mobile.Services;

/// <summary>
/// Thin typed wrapper around <see cref="HttpClient"/> for talking to the ShiftPlanner
/// Web API. The base URL is read fresh from <see cref="AppSettingsStore"/> on every call
/// (so editing the server address in Settings takes effect immediately, no restart needed),
/// and the bearer token is read fresh from <see cref="SecureTokenStore"/> on every call.
/// </summary>
public sealed class ApiClient
{
    // The backend serializes enums as strings (JsonStringEnumConverter registered in
    // Program.cs) — e.g. RosterResponse.DefaultOffDays comes back as ["Saturday","Sunday"],
    // not [6,0]. Mirror that here so DayOfWeek (and any future enum-typed field) deserializes.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Login, attachAuth: false,
            body: new LoginRequest { Email = email, Password = password });

        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new ApiException("The server returned an empty login response.");
        }

        return result;
    }

    public async Task RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Register, attachAuth: false,
            body: new LoginRequest { Email = email, Password = password });

        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    /// <summary>Every team the signed-in account belongs to. Claims any pending admin-added
    /// invites for this email as a side effect (same as the Web app) — call this right after
    /// login so a just-added membership shows up immediately.</summary>
    public async Task<List<TeamSummary>> GetMyTeamsAsync(CancellationToken cancellationToken = default)
    {
        // Team-independent — deliberately does not attach X-Team-Id.
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.TeamsMine, attachAuth: true, attachTeam: false);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<List<TeamSummary>>(JsonOptions, cancellationToken);
        return result ?? new List<TeamSummary>();
    }

    public async Task<TeamSummary> CreateTeamAsync(string name, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Teams, attachAuth: true, attachTeam: false,
            body: new { name });
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<TeamSummary>(JsonOptions, cancellationToken);
        if (result is null) throw new ApiException("The server returned an empty response creating the team.");
        return result;
    }

    /// <summary>The caller's own role and TeamMember record on whichever team is currently
    /// selected (AppSettingsStore.CurrentTeamId — set this before calling).</summary>
    public async Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.MembersMe, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<MeResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>The whole team's roster for one month — entries, team members, and shift types.
    /// The Roster tab derives its day view, "Just me" filter, and shift-assign sheet from this;
    /// there's no per-day or per-team-member filter server-side.</summary>
    public async Task<RosterResponse> GetRosterMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var route = $"{ApiRoutes.Roster}?year={year}&month={month}";
        var request = await CreateRequestAsync(HttpMethod.Get, route, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<RosterResponse>(JsonOptions, cancellationToken);
        return result ?? new RosterResponse();
    }

    /// <summary>Assigns, changes, or clears (shiftCode: null) one team member's shift on one date.
    /// Requires Editor or Admin on the current team — the server returns 403 otherwise.</summary>
    public async Task UpsertRosterEntryAsync(int teamMemberId, DateOnly date, string? shiftCode, string? note = null, CancellationToken cancellationToken = default)
    {
        var body = new RosterEntryUpsertRequest { TeamMemberId = teamMemberId, Date = date, ShiftCode = shiftCode, Note = note };
        var request = await CreateRequestAsync(HttpMethod.Put, ApiRoutes.RosterEntry, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    /// <summary>Assigns the same shift to every (member, date) combination in the cross-product.
    /// Errors are collected per-row rather than aborting the whole batch.</summary>
    public async Task<BulkEntryResult> BulkAssignAsync(List<int> teamMemberIds, List<DateOnly> dates, string? shiftCode, CancellationToken cancellationToken = default)
    {
        var body = new BulkRosterEntryBody { TeamMemberIds = teamMemberIds, Dates = dates, ShiftCode = shiftCode };
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.RosterBulkEntry, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<BulkEntryResult>(JsonOptions, cancellationToken) ?? new BulkEntryResult();
    }

    /// <summary>Applies a per-weekday shift pattern across a whole month for the given members.</summary>
    public async Task<BulkEntryResult> ApplyPatternAsync(ApplyPatternBody body, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.RosterApplyPattern, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<BulkEntryResult>(JsonOptions, cancellationToken) ?? new BulkEntryResult();
    }

    public async Task<RosterPublishStatus> GetRosterPublishStatusAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"{ApiRoutes.RosterPublishStatus}?year={year}&month={month}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<RosterPublishStatus>(JsonOptions, cancellationToken) ?? new RosterPublishStatus();
    }

    public async Task<RosterPublishStatus> PublishRosterAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.RosterPublish}?year={year}&month={month}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<RosterPublishStatus>(JsonOptions, cancellationToken) ?? new RosterPublishStatus();
    }

    public async Task<RosterPublishStatus> UnpublishRosterAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.RosterUnpublish}?year={year}&month={month}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<RosterPublishStatus>(JsonOptions, cancellationToken) ?? new RosterPublishStatus();
    }

    public async Task<List<RosterEntryHistoryRow>> GetRosterHistoryAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"{ApiRoutes.RosterHistory}?year={year}&month={month}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<RosterEntryHistoryRow>>(JsonOptions, cancellationToken) ?? new List<RosterEntryHistoryRow>();
    }

    /// <summary>A "seen it" signal on the caller's own upcoming shift — not a requirement.</summary>
    public async Task AcknowledgeRosterEntryAsync(int entryId, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Patch, $"{ApiRoutes.RosterEntry}/{entryId}/acknowledge", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    // ---- Live availability ----

    public async Task<List<TeamMemberAvailability>> GetTeamAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.TeamAvailability, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<TeamMemberAvailability>>(JsonOptions, cancellationToken) ?? new List<TeamMemberAvailability>();
    }

    public async Task<TeamMemberAvailability> UpdateMyAvailabilityAsync(bool isAvailable, CancellationToken cancellationToken = default)
    {
        var body = new UpdateAvailabilityBody { IsAvailable = isAvailable };
        var request = await CreateRequestAsync(HttpMethod.Patch, ApiRoutes.MyAvailability, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamMemberAvailability>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response updating availability.");
    }

    // ---- Managers ----

    public async Task<List<ManagerAssignment>> GetManagersAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.Managers, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ManagerAssignment>>(JsonOptions, cancellationToken) ?? new List<ManagerAssignment>();
    }

    public async Task<List<PersonSearchResult>> SearchManagerCandidatesAsync(string phone, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"{ApiRoutes.ManagersSearch}?phone={Uri.EscapeDataString(phone)}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<PersonSearchResult>>(JsonOptions, cancellationToken) ?? new List<PersonSearchResult>();
    }

    public async Task<ManagerAssignment> GrantManagerAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var body = new GrantManagerBody { PersonId = personId };
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Managers, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ManagerAssignment>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response granting manager access.");
    }

    public async Task RevokeManagerAsync(int assignmentId, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"{ApiRoutes.Managers}/{assignmentId}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    /// <summary>Every team the signed-in person manages — not team-scoped.</summary>
    public async Task<List<ManagerTeam>> GetManagedTeamsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.ManagerTeams, attachAuth: true, attachTeam: false);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ManagerTeam>>(JsonOptions, cancellationToken) ?? new List<ManagerTeam>();
    }

    /// <summary>Live availability across every team the signed-in person manages — not team-scoped.</summary>
    public async Task<List<ManagerTeamAvailability>> GetManagerAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.ManagerAvailability, attachAuth: true, attachTeam: false);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ManagerTeamAvailability>>(JsonOptions, cancellationToken) ?? new List<ManagerTeamAvailability>();
    }

    // ---- Reports ----

    public async Task<List<UtilizationRow>> GetUtilizationReportAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"{ApiRoutes.ReportsUtilization}?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UtilizationRow>>(JsonOptions, cancellationToken) ?? new List<UtilizationRow>();
    }

    // ---- Leave requests ----

    public async Task<List<LeaveRequest>> GetLeaveRequestsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var route = string.IsNullOrWhiteSpace(status) ? ApiRoutes.LeaveRequests : $"{ApiRoutes.LeaveRequests}?status={status}";
        var request = await CreateRequestAsync(HttpMethod.Get, route, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<LeaveRequest>>(JsonOptions, cancellationToken) ?? new List<LeaveRequest>();
    }

    public async Task<LeaveRequest> CreateLeaveRequestAsync(DateOnly startDate, DateOnly endDate, string? reason, CancellationToken cancellationToken = default)
    {
        var body = new CreateLeaveRequestBody { StartDate = startDate, EndDate = endDate, Reason = reason };
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.LeaveRequests, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LeaveRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response requesting leave.");
    }

    public async Task<LeaveRequest> ApproveLeaveRequestAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.LeaveRequests}/{id}/approve", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LeaveRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response approving the request.");
    }

    public async Task<LeaveRequest> RejectLeaveRequestAsync(int id, string? decisionNote = null, CancellationToken cancellationToken = default)
    {
        var body = new DecideLeaveRequestBody { DecisionNote = decisionNote };
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.LeaveRequests}/{id}/reject", attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LeaveRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response rejecting the request.");
    }

    public async Task<LeaveRequest> CancelLeaveRequestAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.LeaveRequests}/{id}/cancel", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LeaveRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response cancelling the request.");
    }

    // ---- Shift swaps ----

    public async Task<List<ShiftSwapRequest>> GetShiftSwapsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.ShiftSwaps, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ShiftSwapRequest>>(JsonOptions, cancellationToken) ?? new List<ShiftSwapRequest>();
    }

    public async Task<ShiftSwapRequest> CreateShiftSwapAsync(DateOnly date, string shiftCode, int? targetTeamMemberId, CancellationToken cancellationToken = default)
    {
        var body = new CreateShiftSwapBody { Date = date, ShiftCode = shiftCode, TargetTeamMemberId = targetTeamMemberId };
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.ShiftSwaps, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftSwapRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response offering the shift.");
    }

    public async Task<ShiftSwapRequest> ClaimShiftSwapAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.ShiftSwaps}/{id}/claim", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftSwapRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response claiming the swap.");
    }

    public async Task<ShiftSwapRequest> ApproveShiftSwapAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.ShiftSwaps}/{id}/approve", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftSwapRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response approving the swap.");
    }

    public async Task<ShiftSwapRequest> RejectShiftSwapAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.ShiftSwaps}/{id}/reject", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftSwapRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response rejecting the swap.");
    }

    public async Task<ShiftSwapRequest> CancelShiftSwapAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiRoutes.ShiftSwaps}/{id}/cancel", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftSwapRequest>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response cancelling the swap.");
    }

    // ---- Calendar feed ----

    public async Task<string> GetCalendarFeedUrlAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.CalendarFeedUrl, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        return doc.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>Copies a source month's roster onto a target month, by weekday pattern or exact
    /// date. Returns how many entries copied and which need a human look (holiday, inactive
    /// team member, existing leave).</summary>
    public async Task<CopyForwardResult> CopyForwardAsync(CopyForwardRequestBody body, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.RosterCopyForward, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CopyForwardResult>(JsonOptions, cancellationToken) ?? new CopyForwardResult();
    }

    /// <summary>Downloads the month's roster as an .xlsx or .csv file (kind: "excel" or "csv"),
    /// ready to hand to the OS share sheet.</summary>
    public async Task<byte[]> DownloadExportAsync(string kind, int year, int month, CancellationToken cancellationToken = default)
    {
        var route = kind == "excel" ? ApiRoutes.ExportExcel : ApiRoutes.ExportCsv;
        var request = await CreateRequestAsync(HttpMethod.Get, $"{route}?year={year}&month={month}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    // ---- Team members ----

    public async Task<List<TeamMember>> GetTeamMembersAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.Members, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<TeamMember>>(JsonOptions, cancellationToken) ?? new List<TeamMember>();
    }

    /// <summary>A suggested next team member code (e.g. "EMP-004") to pre-fill the add form.</summary>
    public async Task<string> GetNextTeamMemberCodeAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.MembersNextCode, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        return doc.TryGetProperty("code", out var code) ? code.GetString() ?? string.Empty : string.Empty;
    }

    public async Task<TeamMember> CreateTeamMemberAsync(CreateTeamMemberRequest input, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Members, attachAuth: true, body: input);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamMember>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response creating the team member.");
    }

    public async Task<TeamMember> UpdateTeamMemberAsync(int id, UpdateTeamMemberRequest input, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Put, $"{ApiRoutes.Members}/{id}", attachAuth: true, body: input);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamMember>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response updating the team member.");
    }

    public async Task<TeamMember> UpdateMemberRoleAsync(int id, string accessRole, CancellationToken cancellationToken = default)
    {
        var body = new UpdateMemberRoleRequest { AccessRole = accessRole };
        var request = await CreateRequestAsync(HttpMethod.Patch, $"{ApiRoutes.Members}/{id}/role", attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamMember>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response changing the role.");
    }

    public async Task RemoveTeamMemberAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"{ApiRoutes.Members}/{id}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    /// <summary>Transfers the "Lead" label to this member — any Admin can call this (a
    /// Team Settings configuration action), and at most one lead per team is enforced
    /// server-side by unsetting whoever had it before.</summary>
    public async Task<TeamMember> TransferLeadAsync(int memberId, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Patch, $"{ApiRoutes.Members}/{memberId}/lead", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamMember>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response transferring the lead.");
    }

    /// <summary>Sets or clears the "Co-Lead" label — at most one at a time.</summary>
    public async Task<TeamMember> SetCoLeadAsync(int memberId, bool isCoLead, CancellationToken cancellationToken = default)
    {
        var body = new { isCoLead };
        var request = await CreateRequestAsync(HttpMethod.Patch, $"{ApiRoutes.Members}/{memberId}/co-lead", attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamMember>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response changing the co-lead.");
    }

    // ---- Team settings ----

    public async Task<TeamSettings> GetTeamSettingsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.TeamSettings, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamSettings>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response loading team settings.");
    }

    public async Task<TeamSettings> UpdateTeamSettingsAsync(UpdateTeamSettingsRequest input, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Put, ApiRoutes.TeamSettings, attachAuth: true, body: input);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamSettings>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response saving team settings.");
    }

    // ---- Tracks / subtracks ----

    public async Task<List<Track>> GetTracksAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.Tracks, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Track>>(JsonOptions, cancellationToken) ?? new List<Track>();
    }

    public async Task<Track> CreateTrackAsync(TrackInput input, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Tracks, attachAuth: true, body: input);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Track>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response creating the track.");
    }

    public async Task DeleteTrackAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"{ApiRoutes.Tracks}/{id}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public async Task<Subtrack> CreateSubtrackAsync(SubtrackInput input, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.Subtracks, attachAuth: true, body: input);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Subtrack>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response creating the subtrack.");
    }

    public async Task DeleteSubtrackAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"{ApiRoutes.Subtracks}/{id}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    // ---- Job roles / Locations (master lists) ----

    public async Task<List<JobRole>> GetJobRolesAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.JobRoles, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<JobRole>>(JsonOptions, cancellationToken) ?? new List<JobRole>();
    }

    public async Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.Locations, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Location>>(JsonOptions, cancellationToken) ?? new List<Location>();
    }

    // ---- Shift types ----

    public async Task<List<ShiftTypeFull>> GetShiftTypesAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, ApiRoutes.ShiftTypes, attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ShiftTypeFull>>(JsonOptions, cancellationToken) ?? new List<ShiftTypeFull>();
    }

    public async Task<ShiftTypeFull> CreateShiftTypeAsync(ShiftTypeInput input, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, ApiRoutes.ShiftTypes, attachAuth: true, body: input);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftTypeFull>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The server returned an empty response creating the shift type.");
    }

    public async Task DeleteShiftTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, $"{ApiRoutes.ShiftTypes}/{id}", attachAuth: true);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUrl, bool attachAuth, bool attachTeam = true, object? body = null)
    {
        var request = new HttpRequestMessage(method, BuildUri(relativeUrl));

        if (attachAuth)
        {
            var token = await SecureTokenStore.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        if (attachTeam && AppSettingsStore.CurrentTeamId is { } teamId)
        {
            request.Headers.Add("X-Team-Id", teamId.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    private static Uri BuildUri(string relativeUrl)
    {
        var baseUrl = AppSettingsStore.ApiBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ApiException("No API server address is configured. Set it on the Profile tab.");
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new ApiException($"'{baseUrl}' is not a valid server address.");
        }

        return new Uri(baseUri, relativeUrl.TrimStart('/'));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException($"Couldn't reach the server at {AppSettingsStore.ApiBaseUrl}. Check the address on the Profile tab and that the server is running.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid email or password.",
            HttpStatusCode.Forbidden => "You don't have permission to do that on this team.",
            HttpStatusCode.NotFound => "The server endpoint could not be found. Check the API address in Settings.",
            _ => $"Server returned {(int)response.StatusCode} {response.StatusCode}.",
        };

        throw new ApiException(message, (int)response.StatusCode);
    }
}
