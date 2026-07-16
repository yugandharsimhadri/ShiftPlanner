using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
    public async Task UpsertRosterEntryAsync(int teamMemberId, DateOnly date, string? shiftCode, CancellationToken cancellationToken = default)
    {
        var body = new RosterEntryUpsertRequest { TeamMemberId = teamMemberId, Date = date, ShiftCode = shiftCode };
        var request = await CreateRequestAsync(HttpMethod.Put, ApiRoutes.RosterEntry, attachAuth: true, body: body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
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
