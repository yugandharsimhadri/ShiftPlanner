using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class ManagerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ManagerTests(TestWebApplicationFactory factory) => _factory = factory;

    // A person can be granted manager oversight without ever having been added to the
    // team's own roster — "already present" just means the admin already knows them
    // (created them somewhere), matching how "assign to another team" already works.
    private static async Task<UnassignedPersonDto> CreateStandalonePersonAsync(
        HttpClient client, string token, int teamId, string name, string phone, string email)
    {
        var dto = new CreateTeamMemberDto(
            name, phone, email, null, "", null, null, null, null,
            Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today), Models.EmployeeStatus.Active,
            Models.TeamRole.Viewer, new List<int>());
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/members")
        {
            Content = JsonContent.Create(dto, options: JsonOptions)
        }.Authorized(token, teamId);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UnassignedPersonDto>(JsonOptions))!;
    }

    // Unlike CreateStandalonePersonAsync, this actually puts the person on the team's
    // roster (TeamIds includes teamId) — needed to test manager-search clauses that key
    // off actual TeamMember rows, not just "created by."
    private static async Task<TeamMemberDto> CreateRosteredMemberAsync(
        HttpClient client, string token, int teamId, string code, string name, string phone, string email)
    {
        var dto = new CreateTeamMemberDto(
            name, phone, email, null, code, null, null, null, null,
            Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today), Models.EmployeeStatus.Active,
            Models.TeamRole.Viewer, new List<int> { teamId });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/members")
        {
            Content = JsonContent.Create(dto, options: JsonOptions)
        }.Authorized(token, teamId);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamMemberDto>(JsonOptions))!;
    }

    [Fact]
    public async Task Admin_can_search_by_phone_grant_manager_and_the_manager_sees_the_team()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("mgr-admin1@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Manager Team 1");

        var meera = await CreateStandalonePersonAsync(client, adminToken, team.Id, "Meera Nair", "9990001111", "meera1@test.local");

        var searchRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/managers/search?phone=999000")
            .Authorized(adminToken, team.Id);
        var searchResponse = await client.SendAsync(searchRequest);
        var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<PersonSearchResultDto>>(JsonOptions);
        Assert.Contains(searchResults!, p => p.Id == meera.Id);

        var grantRequest = new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/managers")
        {
            Content = JsonContent.Create(new GrantManagerDto(meera.Id), options: JsonOptions)
        }.Authorized(adminToken, team.Id);
        var grantResponse = await client.SendAsync(grantRequest);
        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/managers").Authorized(adminToken, team.Id);
        var managers = await (await client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<ManagerAssignmentDto>>(JsonOptions);
        Assert.Contains(managers!, m => m.PersonId == meera.Id);

        // Meera has never logged in before — this claims her Person the same way an
        // email-invited team member's first login always has.
        var meeraToken = await client.RegisterAndLoginAsync("meera1@test.local");

        var teamsResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/manager/teams").Authorized(meeraToken));
        var managedTeams = await teamsResponse.Content.ReadFromJsonAsync<List<ManagerTeamDto>>(JsonOptions);
        Assert.Contains(managedTeams!, t => t.Id == team.Id);

        var availabilityResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/manager/availability").Authorized(meeraToken));
        var availability = await availabilityResponse.Content.ReadFromJsonAsync<List<ManagerTeamAvailabilityDto>>(JsonOptions);
        var teamEntry = Assert.Single(availability!, t => t.TeamId == team.Id);
        Assert.Contains(teamEntry.Members, m => m.Name == "Admin" || m.Code == "EMP-001");
    }

    [Fact]
    public async Task Revoking_manager_removes_the_teams_dashboard_access()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("mgr-admin2@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Manager Team 2");
        var meera = await CreateStandalonePersonAsync(client, adminToken, team.Id, "Meera Two", "9990002222", "meera2@test.local");

        var grantResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/managers")
        {
            Content = JsonContent.Create(new GrantManagerDto(meera.Id), options: JsonOptions)
        }.Authorized(adminToken, team.Id));
        var assignment = await grantResponse.Content.ReadFromJsonAsync<ManagerAssignmentDto>(JsonOptions);

        var deleteResponse = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/api/teams/current/managers/{assignment!.Id}").Authorized(adminToken, team.Id));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var meeraToken = await client.RegisterAndLoginAsync("meera2@test.local");
        var teamsResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/manager/teams").Authorized(meeraToken));
        var managedTeams = await teamsResponse.Content.ReadFromJsonAsync<List<ManagerTeamDto>>(JsonOptions);
        Assert.DoesNotContain(managedTeams!, t => t.Id == team.Id);
    }

    [Fact]
    public async Task Search_does_not_leak_people_from_another_admins_teams()
    {
        var client = _factory.CreateClient();
        var adminAToken = await client.RegisterAndLoginAsync("mgr-admin-a@test.local");
        var adminBToken = await client.RegisterAndLoginAsync("mgr-admin-b@test.local");
        var teamA = await client.CreateTeamAsync(adminAToken, "Manager Team A");
        var teamB = await client.CreateTeamAsync(adminBToken, "Manager Team B");

        await CreateStandalonePersonAsync(client, adminBToken, teamB.Id, "Only In B", "9990003333", "onlyinb@test.local");

        var searchRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/managers/search?phone=999000")
            .Authorized(adminAToken, teamA.Id);
        var results = await (await client.SendAsync(searchRequest)).Content.ReadFromJsonAsync<List<PersonSearchResultDto>>(JsonOptions);

        Assert.DoesNotContain(results!, p => p.Name == "Only In B");
    }

    [Fact]
    public async Task Search_includes_people_from_every_team_the_caller_administers()
    {
        var client = _factory.CreateClient();
        var adminAToken = await client.RegisterAndLoginAsync("mgr-org-a@test.local");
        var adminBToken = await client.RegisterAndLoginAsync("mgr-org-b@test.local");
        var teamA = await client.CreateTeamAsync(adminAToken, "Org Team A");
        var teamB = await client.CreateTeamAsync(adminBToken, "Org Team B");

        // A person on Team B's actual roster, created by adminB — Team A's admin doesn't
        // yet administer Team B, so this shouldn't be searchable from Team A.
        var crossOrgPerson = await CreateRosteredMemberAsync(client, adminBToken, teamB.Id, "EMP-002", "Cross Org Person", "9990005555", "crossorg@test.local");

        var beforeSearch = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/managers/search?phone=999000")
            .Authorized(adminAToken, teamA.Id))).Content.ReadFromJsonAsync<List<PersonSearchResultDto>>(JsonOptions);
        Assert.DoesNotContain(beforeSearch!, p => p.Id == crossOrgPerson.PersonId);

        // adminB invites adminA onto Team B and promotes them to Admin there.
        var inviteResponse = await client.InviteMemberAsync(adminBToken, teamB.Id, "mgr-org-a@test.local", "Viewer", "EMP-003");
        var invitedMember = await inviteResponse.Content.ReadFromJsonAsync<TeamMemberDto>(JsonOptions);
        var promoteRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/teams/current/members/{invitedMember!.Id}/role")
        {
            Content = JsonContent.Create(new UpdateMemberRoleDto(Models.TeamRole.Admin), options: JsonOptions)
        }.Authorized(adminBToken, teamB.Id);
        (await client.SendAsync(promoteRequest)).EnsureSuccessStatusCode();

        // Now that adminA is also an Admin on Team B, the same search from Team A should
        // surface a person who's on Team B, even though adminA didn't create them.
        var afterSearch = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/managers/search?phone=999000")
            .Authorized(adminAToken, teamA.Id))).Content.ReadFromJsonAsync<List<PersonSearchResultDto>>(JsonOptions);
        Assert.Contains(afterSearch!, p => p.Id == crossOrgPerson.PersonId);
    }

    [Fact]
    public async Task Editor_cannot_grant_manager_access()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("mgr-admin3@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Manager Team 3");
        var meera = await CreateStandalonePersonAsync(client, adminToken, team.Id, "Meera Three", "9990004444", "meera3@test.local");

        var editorAddResponse = await client.InviteMemberAsync(adminToken, team.Id, "editor3@test.local", "Editor");
        editorAddResponse.EnsureSuccessStatusCode();
        var editorToken = await client.RegisterAndLoginAsync("editor3@test.local");

        var grantRequest = new HttpRequestMessage(HttpMethod.Post, "/api/teams/current/managers")
        {
            Content = JsonContent.Create(new GrantManagerDto(meera.Id), options: JsonOptions)
        }.Authorized(editorToken, team.Id);
        var grantResponse = await client.SendAsync(grantRequest);

        Assert.Equal(HttpStatusCode.Forbidden, grantResponse.StatusCode);
    }
}
