using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShiftPlanner.Api.Dtos;

namespace ShiftPlanner.Api.Tests;

/// <summary>Thin helpers over HttpClient so tests read as scenarios, not HTTP plumbing.</summary>
public static class ApiTestClient
{
    // Mirrors the API's own JSON configuration (Program.cs: web defaults + string enums).
    // System.Net.Http.Json's Read/JsonContent helpers do NOT pick that up automatically —
    // without this, a PascalCase record property or an enum would silently bind to
    // null/default instead of failing loudly, on both the request and response side.
    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record LoginResult(string TokenType, string AccessToken, double ExpiresIn, string? RefreshToken);

    public static async Task RegisterAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/register", new { email, password }, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<string> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/login", new { email, password }, JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResult>(JsonOptions);
        return result!.AccessToken;
    }

    public static async Task<string> RegisterAndLoginAsync(this HttpClient client, string email, string password = "Passw0rd!")
    {
        await client.RegisterAsync(email, password);
        return await client.LoginAsync(email, password);
    }

    public static HttpRequestMessage Authorized(this HttpRequestMessage request, string token, int? teamId = null)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (teamId is not null) request.Headers.Add("X-Team-Id", teamId.Value.ToString());
        return request;
    }

    public static async Task<TeamSummaryDto> CreateTeamAsync(this HttpClient client, string token, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teams") { Content = JsonContent.Create(new CreateTeamDto(name), options: JsonOptions) }
            .Authorized(token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamSummaryDto>(JsonOptions))!;
    }

    public static async Task<List<TeamSummaryDto>> GetMyTeamsAsync(this HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/teams/mine").Authorized(token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<TeamSummaryDto>>(JsonOptions))!;
    }

    public static async Task<HttpResponseMessage> AddMemberAsync(this HttpClient client, string token, int teamId, string email, string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/members")
        {
            Content = JsonContent.Create(new { email, role }, options: JsonOptions)
        }.Authorized(token, teamId);
        return await client.SendAsync(request);
    }

    public static async Task<Models.Track> CreateTrackAsync(this HttpClient client, string token, int teamId, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tracks")
        {
            Content = JsonContent.Create(new TrackDto(null, name, null, "#4453AD"), options: JsonOptions)
        }.Authorized(token, teamId);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Models.Track>(JsonOptions))!;
    }

    public static async Task<HttpResponseMessage> CreateEmployeeAsync(this HttpClient client, string token, int teamId, string code, string name, int trackId)
    {
        var dto = new EmployeeDto(code, name, "555-0100", null, trackId, null, "Clerk",
            Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today), Models.EmployeeStatus.Active, null);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = JsonContent.Create(dto, options: JsonOptions) }
            .Authorized(token, teamId);
        return await client.SendAsync(request);
    }
}
