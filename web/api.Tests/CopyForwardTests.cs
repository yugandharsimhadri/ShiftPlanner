using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class CopyForwardTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CopyForwardTests(TestWebApplicationFactory factory) => _factory = factory;

    private static async Task SetShiftAsync(HttpClient client, string token, int teamId, int teamMemberId, DateOnly date, string? code)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/roster/entry")
        {
            Content = JsonContent.Create(new RosterEntryUpsertDto(teamMemberId, date, code, null), options: JsonOptions)
        }.Authorized(token, teamId);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Weekday_pattern_maps_the_nth_occurrence_of_the_weekday_to_the_same_nth_occurrence()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("copyfwd1@test.local");
        var team = await client.CreateTeamAsync(token, "Copy Team 1");
        var track = await client.CreateTrackAsync(token, team.Id, "Ops");

        var createMember = await client.CreateTeamMemberAsync(token, team.Id, "EMP-002", "Alice", track.Id);
        var member = await createMember.Content.ReadFromJsonAsync<TeamMemberDto>(JsonOptions);

        var shiftTypeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/shift-types")
        {
            Content = JsonContent.Create(new ShiftTypeDto(null, "M", "Morning", null, null, "#A8701F", false, true), options: JsonOptions)
        }.Authorized(token, team.Id);
        await client.SendAsync(shiftTypeRequest);

        // 2nd Thursday of Jan 2026 is Jan 8.
        var sourceDate = new DateOnly(2026, 1, 8);
        await SetShiftAsync(client, token, team.Id, member!.Id, sourceDate, "M");

        var copyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/roster/copy-forward")
        {
            Content = JsonContent.Create(new CopyForwardRequest(2026, 1, 2026, 2, "weekday", true), options: JsonOptions)
        }.Authorized(token, team.Id);
        var copyResponse = await client.SendAsync(copyRequest);
        copyResponse.EnsureSuccessStatusCode();
        var result = await copyResponse.Content.ReadFromJsonAsync<CopyForwardResult>(JsonOptions);

        // 2nd Thursday of Feb 2026 is Feb 12.
        var rosterRequest = new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=2").Authorized(token, team.Id);
        var rosterResponse = await client.SendAsync(rosterRequest);
        var roster = await rosterResponse.Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);

        var copiedEntry = roster!.Entries.SingleOrDefault(e => e.TeamMemberId == member.Id && e.Date == new DateOnly(2026, 2, 12));
        Assert.NotNull(copiedEntry);
        Assert.Equal("M", copiedEntry!.ShiftCode);
        Assert.True(result!.CopiedCount >= 1);
    }

    [Fact]
    public async Task Inactive_team_members_are_skipped_when_requested()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("copyfwd2@test.local");
        var team = await client.CreateTeamAsync(token, "Copy Team 2");
        var track = await client.CreateTrackAsync(token, team.Id, "Ops");

        var shiftTypeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/shift-types")
        {
            Content = JsonContent.Create(new ShiftTypeDto(null, "M", "Morning", null, null, "#A8701F", false, true), options: JsonOptions)
        }.Authorized(token, team.Id);
        await client.SendAsync(shiftTypeRequest);

        var createMember = await client.CreateTeamMemberAsync(token, team.Id, "EMP-002", "Alice", track.Id);
        var member = await createMember.Content.ReadFromJsonAsync<TeamMemberDto>(JsonOptions);
        await SetShiftAsync(client, token, team.Id, member!.Id, new DateOnly(2026, 3, 2), "M");

        // Deactivate.
        var deactivateDto = new UpdateTeamMemberDto("Alice", "555-0100", null, null, "EMP-002", track.Id, null, "Clerk", null,
            Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today),
            Models.EmployeeStatus.Inactive, Models.TeamRole.Viewer);
        var deactivateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/teams/current/members/{member.Id}")
        {
            Content = JsonContent.Create(deactivateDto, options: JsonOptions)
        }.Authorized(token, team.Id);
        (await client.SendAsync(deactivateRequest)).EnsureSuccessStatusCode();

        var copyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/roster/copy-forward")
        {
            Content = JsonContent.Create(new CopyForwardRequest(2026, 3, 2026, 4, "exact-date", true), options: JsonOptions)
        }.Authorized(token, team.Id);
        var copyResponse = await client.SendAsync(copyRequest);
        var result = await copyResponse.Content.ReadFromJsonAsync<CopyForwardResult>(JsonOptions);

        Assert.Equal(0, result!.CopiedCount);
    }

    private sealed record RosterResponseShape(List<RosterEntryShape> Entries);
    private sealed record RosterEntryShape(int TeamMemberId, DateOnly Date, string? ShiftCode);
}
