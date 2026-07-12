using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class MembershipLinkTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MembershipLinkTests(TestWebApplicationFactory factory) => _factory = factory;

    private sealed record MemberRow(int Id, string Email, string Role, string Status, Guid? EmployeeId);

    [Fact]
    public async Task Admin_can_link_a_member_to_an_employee_and_the_member_sees_it_via_me()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("link-admin1@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Link Team 1");
        var track = await client.CreateTrackAsync(adminToken, team.Id, "Ops");

        var createEmp = await client.CreateEmployeeAsync(adminToken, team.Id, "EMP-001", "Priya Nair", track.Id);
        var employee = await createEmp.Content.ReadFromJsonAsync<ShiftPlanner.Api.Models.Employee>(JsonOptions);

        await client.AddMemberAsync(adminToken, team.Id, "member1@test.local", "Viewer");
        var memberToken = await client.RegisterAndLoginAsync("member1@test.local");

        var membersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, team.Id);
        var membersResponse = await client.SendAsync(membersRequest);
        var members = await membersResponse.Content.ReadFromJsonAsync<List<MemberRow>>(JsonOptions);
        var membership = members!.Single(m => m.Email == "member1@test.local");

        var linkRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/teams/current/members/{membership.Id}/employee")
        {
            Content = JsonContent.Create(new LinkEmployeeDto(employee!.Id), options: JsonOptions)
        }.Authorized(adminToken, team.Id);
        var linkResponse = await client.SendAsync(linkRequest);
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members/me").Authorized(memberToken, team.Id);
        var meResponse = await client.SendAsync(meRequest);
        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>(JsonOptions);

        Assert.Equal(employee.Id, me!.EmployeeId);
        Assert.Equal("EMP-001", me.EmployeeCode);
    }

    [Fact]
    public async Task Non_admin_cannot_link_a_member_to_an_employee()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("link-admin2@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Link Team 2");
        var track = await client.CreateTrackAsync(adminToken, team.Id, "Ops");

        var createEmp = await client.CreateEmployeeAsync(adminToken, team.Id, "EMP-001", "Priya Nair", track.Id);
        var employee = await createEmp.Content.ReadFromJsonAsync<ShiftPlanner.Api.Models.Employee>(JsonOptions);

        await client.AddMemberAsync(adminToken, team.Id, "editor2@test.local", "Editor");
        var editorToken = await client.RegisterAndLoginAsync("editor2@test.local");

        var membersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, team.Id);
        var membersResponse = await client.SendAsync(membersRequest);
        var members = await membersResponse.Content.ReadFromJsonAsync<List<MemberRow>>(JsonOptions);
        var membership = members!.Single(m => m.Email == "editor2@test.local");

        var linkRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/teams/current/members/{membership.Id}/employee")
        {
            Content = JsonContent.Create(new LinkEmployeeDto(employee!.Id), options: JsonOptions)
        }.Authorized(editorToken, team.Id);
        var linkResponse = await client.SendAsync(linkRequest);

        Assert.Equal(HttpStatusCode.Forbidden, linkResponse.StatusCode);
    }

    [Fact]
    public async Task Linking_to_an_employee_from_a_different_team_is_rejected()
    {
        var client = _factory.CreateClient();
        var adminAToken = await client.RegisterAndLoginAsync("link-admin3a@test.local");
        var adminBToken = await client.RegisterAndLoginAsync("link-admin3b@test.local");
        var teamA = await client.CreateTeamAsync(adminAToken, "Link Team 3A");
        var teamB = await client.CreateTeamAsync(adminBToken, "Link Team 3B");
        var trackB = await client.CreateTrackAsync(adminBToken, teamB.Id, "Ops");

        var createEmpB = await client.CreateEmployeeAsync(adminBToken, teamB.Id, "EMP-001", "Bob", trackB.Id);
        var employeeB = await createEmpB.Content.ReadFromJsonAsync<ShiftPlanner.Api.Models.Employee>(JsonOptions);

        await client.AddMemberAsync(adminAToken, teamA.Id, "viewer3@test.local", "Viewer");
        var membersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminAToken, teamA.Id);
        var membersResponse = await client.SendAsync(membersRequest);
        var members = await membersResponse.Content.ReadFromJsonAsync<List<MemberRow>>(JsonOptions);
        var membership = members!.Single(m => m.Email == "viewer3@test.local");

        var linkRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/teams/current/members/{membership.Id}/employee")
        {
            Content = JsonContent.Create(new LinkEmployeeDto(employeeB!.Id), options: JsonOptions)
        }.Authorized(adminAToken, teamA.Id);
        var linkResponse = await client.SendAsync(linkRequest);

        Assert.Equal(HttpStatusCode.BadRequest, linkResponse.StatusCode);
    }

    [Fact]
    public async Task Unlinked_member_gets_null_employee_code_from_me()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("link-admin4@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Link Team 4");

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members/me").Authorized(adminToken, team.Id);
        var meResponse = await client.SendAsync(meRequest);
        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>(JsonOptions);

        Assert.Null(me!.EmployeeId);
        Assert.Null(me.EmployeeCode);
    }
}
