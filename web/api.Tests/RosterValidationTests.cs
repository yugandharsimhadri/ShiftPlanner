using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class RosterValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RosterValidationTests(TestWebApplicationFactory factory) => _factory = factory;

    private static async Task CreateWorkShiftTypeAsync(HttpClient client, string token, int teamId, string code = "M")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/shift-types")
        {
            Content = JsonContent.Create(new ShiftTypeDto(null, code, "Morning", null, null, "#A8701F", false, true), options: JsonOptions)
        }.Authorized(token, teamId);
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Assigning_a_shift_to_an_inactive_member_is_rejected()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("rosterval1@test.local");
        var team = await client.CreateTeamAsync(token, "Validation Team 1");
        var track = await client.CreateTrackAsync(token, team.Id, "Ops");
        await CreateWorkShiftTypeAsync(client, token, team.Id);

        var createMember = await client.CreateTeamMemberAsync(token, team.Id, "EMP-002", "Alice", track.Id);
        var member = await createMember.Content.ReadFromJsonAsync<TeamMemberDto>(JsonOptions);

        var deactivateDto = new UpdateTeamMemberDto("Alice", "555-0100", null, null, "EMP-002", track.Id, null, null, null,
            Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today),
            Models.EmployeeStatus.Inactive, Models.TeamRole.Viewer);
        var deactivateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/teams/current/members/{member!.Id}")
        {
            Content = JsonContent.Create(deactivateDto, options: JsonOptions)
        }.Authorized(token, team.Id);
        (await client.SendAsync(deactivateRequest)).EnsureSuccessStatusCode();

        var entryRequest = new HttpRequestMessage(HttpMethod.Put, "/api/roster/entry")
        {
            Content = JsonContent.Create(new RosterEntryUpsertDto(member.Id, new DateOnly(2026, 3, 2), "M", null), options: JsonOptions)
        }.Authorized(token, team.Id);
        var response = await client.SendAsync(entryRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_sees_no_entries_for_an_unpublished_month_but_editor_sees_the_draft()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("rosterval2@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Validation Team 2");
        var track = await client.CreateTrackAsync(adminToken, team.Id, "Ops");
        await CreateWorkShiftTypeAsync(client, adminToken, team.Id);

        var createMember = await client.CreateTeamMemberAsync(adminToken, team.Id, "EMP-002", "Bob", track.Id);
        var member = await createMember.Content.ReadFromJsonAsync<TeamMemberDto>(JsonOptions);

        var entryRequest = new HttpRequestMessage(HttpMethod.Put, "/api/roster/entry")
        {
            Content = JsonContent.Create(new RosterEntryUpsertDto(member!.Id, new DateOnly(2026, 5, 4), "M", null), options: JsonOptions)
        }.Authorized(adminToken, team.Id);
        (await client.SendAsync(entryRequest)).EnsureSuccessStatusCode();

        await client.InviteMemberAsync(adminToken, team.Id, "viewer2@test.local", "Viewer", "EMP-003");
        var viewerToken = await client.RegisterAndLoginAsync("viewer2@test.local");

        var viewerRosterBefore = await (await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=5").Authorized(viewerToken, team.Id)))
            .Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);
        Assert.False(viewerRosterBefore!.IsPublished);
        Assert.Empty(viewerRosterBefore.Entries);

        var adminRosterBefore = await (await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=5").Authorized(adminToken, team.Id)))
            .Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);
        Assert.NotEmpty(adminRosterBefore!.Entries);

        var publishRequest = new HttpRequestMessage(HttpMethod.Post, "/api/roster/publish?year=2026&month=5").Authorized(adminToken, team.Id);
        (await client.SendAsync(publishRequest)).EnsureSuccessStatusCode();

        var viewerRosterAfter = await (await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=5").Authorized(viewerToken, team.Id)))
            .Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);
        Assert.True(viewerRosterAfter!.IsPublished);
        Assert.NotEmpty(viewerRosterAfter.Entries);
    }

    private sealed record RosterResponseShape(List<RosterEntryShape> Entries, bool IsPublished);
    private sealed record RosterEntryShape(int TeamMemberId, DateOnly Date, string? ShiftCode);
}
