using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

// Covers what used to be a separate "link a membership to an employee" feature — now that
// a TeamMember row *is* the roster record, there's nothing to link. What still needs
// covering is the claim: a team member added by email before they've ever signed in should
// pick up their login automatically the first time they do, and /me should reflect them.
public class TeamMemberLoginClaimTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TeamMemberLoginClaimTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task A_team_member_added_by_email_is_claimed_on_first_login_and_visible_via_me()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("claim-admin1@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Claim Team 1");
        var track = await client.CreateTrackAsync(adminToken, team.Id, "Ops");

        var createResponse = await client.CreateTeamMemberAsync(
            adminToken, team.Id, "EMP-002", "Priya Nair", track.Id, accessRole: "Editor", email: "priya@test.local");
        createResponse.EnsureSuccessStatusCode();

        // Priya has never logged in before — this first login should claim her Person row.
        var priyaToken = await client.RegisterAndLoginAsync("priya@test.local");

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members/me").Authorized(priyaToken, team.Id);
        var meResponse = await client.SendAsync(meRequest);
        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>(JsonOptions);

        Assert.Equal("EMP-002", me!.Code);
        Assert.Equal(Models.TeamRole.Editor, me.Role);
    }

    [Fact]
    public async Task Team_member_created_with_no_teams_is_recorded_but_unassigned()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("claim-admin2@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Claim Team 2");

        var dto = new CreateTeamMemberDto(
            "Arjun Mehta", "555-0199", null, null, "", null, null, null, null,
            Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today), Models.EmployeeStatus.Active,
            Models.TeamRole.Viewer, new List<int>());
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/members")
        {
            Content = JsonContent.Create(dto, options: JsonOptions)
        }.Authorized(adminToken, team.Id);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var candidatesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members/unassigned-candidates").Authorized(adminToken, team.Id);
        var candidatesResponse = await client.SendAsync(candidatesRequest);
        var candidates = await candidatesResponse.Content.ReadFromJsonAsync<List<UnassignedPersonDto>>(JsonOptions);

        Assert.Contains(candidates!, p => p.Name == "Arjun Mehta");
    }
}
