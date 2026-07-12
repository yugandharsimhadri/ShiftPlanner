using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ShiftPlanner.Api.Tests;

public class RoleEnforcementTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RoleEnforcementTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient client, string adminToken, int teamId, int trackId)> SetUpTeamAsync(string suffix)
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync($"admin-{suffix}@test.local");
        var team = await client.CreateTeamAsync(adminToken, $"Team {suffix}");
        var track = await client.CreateTrackAsync(adminToken, team.Id, "Ops");
        return (client, adminToken, team.Id, track.Id);
    }

    private static async Task<string> InviteAndLoginAsAsync(HttpClient client, string adminToken, int teamId, string email, string role)
    {
        var addResult = await client.InviteMemberAsync(adminToken, teamId, email, role);
        addResult.EnsureSuccessStatusCode();
        return await client.RegisterAndLoginAsync(email);
    }

    [Fact]
    public async Task Viewer_cannot_create_a_team_member()
    {
        var (client, adminToken, teamId, trackId) = await SetUpTeamAsync("viewer-emp");
        var viewerToken = await InviteAndLoginAsAsync(client, adminToken, teamId, "viewer1@test.local", "Viewer");

        var response = await client.CreateTeamMemberAsync(viewerToken, teamId, "EMP-002", "Alice", trackId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_can_read_the_roster()
    {
        var (client, adminToken, teamId, _) = await SetUpTeamAsync("viewer-read");
        var viewerToken = await InviteAndLoginAsAsync(client, adminToken, teamId, "viewer2@test.local", "Viewer");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(viewerToken, teamId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Editor_cannot_add_or_manage_team_members()
    {
        // Team member management (which doubles as access-granting) is Admin-only —
        // unlike the old split model, Editor no longer gets a lighter "add employees" path.
        var (client, adminToken, teamId, trackId) = await SetUpTeamAsync("editor");
        var editorToken = await InviteAndLoginAsAsync(client, adminToken, teamId, "editor1@test.local", "Editor");

        var createAttempt = await client.CreateTeamMemberAsync(editorToken, teamId, "EMP-002", "Alice", trackId);
        Assert.Equal(HttpStatusCode.Forbidden, createAttempt.StatusCode);
    }

    [Fact]
    public async Task Only_an_admin_can_change_a_members_role()
    {
        var (client, adminToken, teamId, _) = await SetUpTeamAsync("role-change");
        var editorToken = await InviteAndLoginAsAsync(client, adminToken, teamId, "editor2@test.local", "Editor");

        var members = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, teamId);
        var membersResponse = await client.SendAsync(members);
        var list = await membersResponse.Content.ReadFromJsonAsync<List<MemberRow>>(ApiTestClient.JsonOptions);
        var editorMember = list!.Single(m => m.Name == "editor2@test.local");

        // Editor tries to promote themselves — should fail.
        var selfPromote = new HttpRequestMessage(HttpMethod.Patch, $"/api/teams/current/members/{editorMember.Id}/role")
        {
            Content = JsonContent.Create(new { accessRole = "Admin" }, options: ApiTestClient.JsonOptions)
        }.Authorized(editorToken, teamId);
        var selfPromoteResponse = await client.SendAsync(selfPromote);
        Assert.Equal(HttpStatusCode.Forbidden, selfPromoteResponse.StatusCode);

        // Admin promotes them — should succeed.
        var adminPromote = new HttpRequestMessage(HttpMethod.Patch, $"/api/teams/current/members/{editorMember.Id}/role")
        {
            Content = JsonContent.Create(new { accessRole = "Admin" }, options: ApiTestClient.JsonOptions)
        }.Authorized(adminToken, teamId);
        var adminPromoteResponse = await client.SendAsync(adminPromote);
        Assert.Equal(HttpStatusCode.OK, adminPromoteResponse.StatusCode);
    }

    [Fact]
    public async Task Cannot_remove_the_last_admin_of_a_team()
    {
        var (client, adminToken, teamId, _) = await SetUpTeamAsync("last-admin");

        var members = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, teamId);
        var membersResponse = await client.SendAsync(members);
        var list = await membersResponse.Content.ReadFromJsonAsync<List<MemberRow>>(ApiTestClient.JsonOptions);
        var selfMember = list!.Single();

        var removeSelf = new HttpRequestMessage(HttpMethod.Delete, $"/api/teams/current/members/{selfMember.Id}").Authorized(adminToken, teamId);
        var response = await client.SendAsync(removeSelf);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record MemberRow(int Id, string Name, string AccessRole, bool IsTeamLead);
}
