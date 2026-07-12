using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class CopyForwardTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CopyForwardTests(TestWebApplicationFactory factory) => _factory = factory;

    private static async Task SetShiftAsync(HttpClient client, string token, int teamId, Guid employeeId, DateOnly date, string? code)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/roster/entry")
        {
            Content = JsonContent.Create(new RosterEntryUpsertDto(employeeId, date, code, null), options: JsonOptions)
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

        var createEmp = await client.CreateEmployeeAsync(token, team.Id, "EMP-001", "Alice", track.Id);
        var employee = await createEmp.Content.ReadFromJsonAsync<ShiftPlanner.Api.Models.Employee>(JsonOptions);

        var shiftTypeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/shift-types")
        {
            Content = JsonContent.Create(new ShiftTypeDto(null, "M", "Morning", null, null, "#A8701F", false), options: JsonOptions)
        }.Authorized(token, team.Id);
        await client.SendAsync(shiftTypeRequest);

        // 2nd Thursday of Jan 2026 is Jan 8.
        var sourceDate = new DateOnly(2026, 1, 8);
        await SetShiftAsync(client, token, team.Id, employee!.Id, sourceDate, "M");

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

        var copiedEntry = roster!.Entries.SingleOrDefault(e => e.EmployeeId == employee.Id && e.Date == new DateOnly(2026, 2, 12));
        Assert.NotNull(copiedEntry);
        Assert.Equal("M", copiedEntry!.ShiftCode);
        Assert.True(result!.CopiedCount >= 1);
    }

    [Fact]
    public async Task Inactive_employees_are_skipped_when_requested()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("copyfwd2@test.local");
        var team = await client.CreateTeamAsync(token, "Copy Team 2");
        var track = await client.CreateTrackAsync(token, team.Id, "Ops");

        var shiftTypeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/shift-types")
        {
            Content = JsonContent.Create(new ShiftTypeDto(null, "M", "Morning", null, null, "#A8701F", false), options: JsonOptions)
        }.Authorized(token, team.Id);
        await client.SendAsync(shiftTypeRequest);

        var createEmp = await client.CreateEmployeeAsync(token, team.Id, "EMP-001", "Alice", track.Id);
        var employee = await createEmp.Content.ReadFromJsonAsync<ShiftPlanner.Api.Models.Employee>(JsonOptions);
        await SetShiftAsync(client, token, team.Id, employee!.Id, new DateOnly(2026, 3, 2), "M");

        // Deactivate.
        var deactivateDto = new EmployeeDto("EMP-001", "Alice", "555-0100", null, track.Id, null, "Clerk",
            ShiftPlanner.Api.Models.EmploymentType.FullTime, DateOnly.FromDateTime(DateTime.Today), null,
            ShiftPlanner.Api.Models.EmployeeStatus.Inactive, null);
        var deactivateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/employees/{employee.Id}")
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
    private sealed record RosterEntryShape(Guid EmployeeId, DateOnly Date, string? ShiftCode);
}
