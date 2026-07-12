using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Models;
using Xunit;

namespace ShiftPlanner.Api.Tests;

public class MultiTenantIsolationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MultiTenantIsolationTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Creating_a_team_makes_the_creator_its_admin()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("owner1@test.local");

        var team = await client.CreateTeamAsync(token, "Team One");

        Assert.Equal(TeamRole.Admin, team.Role);
        Assert.Equal("Team One", team.Name);
    }

    [Fact]
    public async Task Team_B_cannot_see_Team_As_employees()
    {
        var client = _factory.CreateClient();
        var tokenA = await client.RegisterAndLoginAsync("owner-a1@test.local");
        var tokenB = await client.RegisterAndLoginAsync("owner-b1@test.local");

        var teamA = await client.CreateTeamAsync(tokenA, "Team A1");
        var teamB = await client.CreateTeamAsync(tokenB, "Team B1");

        var track = await client.CreateTrackAsync(tokenA, teamA.Id, "Ops");
        var createResponse = await client.CreateEmployeeAsync(tokenA, teamA.Id, "EMP-001", "Alice", track.Id);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/employees").Authorized(tokenB, teamB.Id);
        var listResponse = await client.SendAsync(listRequest);
        var employees = await listResponse.Content.ReadFromJsonAsync<List<Employee>>(ApiTestClient.JsonOptions);

        Assert.Empty(employees!);
    }

    [Fact]
    public async Task A_member_of_Team_A_is_forbidden_from_reading_Team_B_with_Team_Bs_id()
    {
        var client = _factory.CreateClient();
        var tokenA = await client.RegisterAndLoginAsync("owner-a2@test.local");
        var tokenB = await client.RegisterAndLoginAsync("owner-b2@test.local");

        var teamA = await client.CreateTeamAsync(tokenA, "Team A2");
        var teamB = await client.CreateTeamAsync(tokenB, "Team B2");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/employees").Authorized(tokenA, teamB.Id);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Requests_without_a_team_header_are_rejected()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("noteam@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/employees").Authorized(token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Same_email_invited_to_two_teams_gets_both_with_independent_roles_after_first_login()
    {
        var client = _factory.CreateClient();
        var tokenA = await client.RegisterAndLoginAsync("owner-a3@test.local");
        var tokenB = await client.RegisterAndLoginAsync("owner-b3@test.local");

        var teamA = await client.CreateTeamAsync(tokenA, "Team A3");
        var teamB = await client.CreateTeamAsync(tokenB, "Team B3");

        var addToA = await client.AddMemberAsync(tokenA, teamA.Id, "shared3@test.local", "Editor");
        var addToB = await client.AddMemberAsync(tokenB, teamB.Id, "shared3@test.local", "Viewer");
        Assert.True(addToA.IsSuccessStatusCode);
        Assert.True(addToB.IsSuccessStatusCode);

        // The shared account has never logged in before now — this first login is what
        // should claim both pending invites.
        var sharedToken = await client.RegisterAndLoginAsync("shared3@test.local");
        var teams = await client.GetMyTeamsAsync(sharedToken);

        Assert.Equal(2, teams.Count);
        Assert.Equal(TeamRole.Editor, teams.Single(t => t.Id == teamA.Id).Role);
        Assert.Equal(TeamRole.Viewer, teams.Single(t => t.Id == teamB.Id).Role);
    }

    [Fact]
    public async Task Employee_code_only_needs_to_be_unique_within_a_team_not_globally()
    {
        var client = _factory.CreateClient();
        var tokenA = await client.RegisterAndLoginAsync("owner-a4@test.local");
        var tokenB = await client.RegisterAndLoginAsync("owner-b4@test.local");

        var teamA = await client.CreateTeamAsync(tokenA, "Team A4");
        var teamB = await client.CreateTeamAsync(tokenB, "Team B4");

        var trackA = await client.CreateTrackAsync(tokenA, teamA.Id, "Ops");
        var trackB = await client.CreateTrackAsync(tokenB, teamB.Id, "Ops");

        var first = await client.CreateEmployeeAsync(tokenA, teamA.Id, "EMP-001", "Alice", trackA.Id);
        var second = await client.CreateEmployeeAsync(tokenB, teamB.Id, "EMP-001", "Bob", trackB.Id);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Employee_code_must_be_unique_within_the_same_team()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("owner-dup@test.local");
        var team = await client.CreateTeamAsync(token, "Team Dup");
        var track = await client.CreateTrackAsync(token, team.Id, "Ops");

        var first = await client.CreateEmployeeAsync(token, team.Id, "EMP-001", "Alice", track.Id);
        var second = await client.CreateEmployeeAsync(token, team.Id, "EMP-001", "Bob", track.Id);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
