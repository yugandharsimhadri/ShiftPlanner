using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class LeaveRequestTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public LeaveRequestTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Member_requests_leave_admin_approves_and_it_appears_on_the_roster()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("leave1-admin@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Leave Team 1");
        await client.SetAutoApproveAsync(adminToken, team.Id, autoApproveLeaveRequests: false, autoApproveShiftSwaps: false);

        await client.InviteMemberAsync(adminToken, team.Id, "leave1-member@test.local", "Viewer", "EMP-002");
        var memberToken = await client.RegisterAndLoginAsync("leave1-member@test.local");

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/leave-requests")
        {
            Content = JsonContent.Create(new CreateLeaveRequestDto(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12), "Family trip"), options: JsonOptions)
        }.Authorized(memberToken, team.Id);
        var createResponse = await client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var leave = await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);
        Assert.Equal(Models.LeaveStatus.Pending, leave!.Status);

        // A Viewer can't approve their own (or anyone's) request.
        var selfApprove = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/leave-requests/{leave.Id}/approve").Authorized(memberToken, team.Id));
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);

        var approveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/leave-requests/{leave.Id}/approve").Authorized(adminToken, team.Id);
        var approveResponse = await client.SendAsync(approveRequest);
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);
        Assert.Equal(Models.LeaveStatus.Approved, approved!.Status);

        var rosterRequest = new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=6").Authorized(adminToken, team.Id);
        var roster = await (await client.SendAsync(rosterRequest)).Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);

        Assert.Contains(roster!.LeaveRequests, l => l.Id == leave.Id && l.Status == Models.LeaveStatus.Approved);
    }

    [Fact]
    public async Task Admin_can_reject_a_pending_request_and_it_cannot_be_decided_twice()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("leave2-admin@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Leave Team 2");
        await client.SetAutoApproveAsync(adminToken, team.Id, autoApproveLeaveRequests: false, autoApproveShiftSwaps: false);
        await client.InviteMemberAsync(adminToken, team.Id, "leave2-member@test.local", "Viewer", "EMP-002");
        var memberToken = await client.RegisterAndLoginAsync("leave2-member@test.local");

        var createResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/leave-requests")
        {
            Content = JsonContent.Create(new CreateLeaveRequestDto(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2), null), options: JsonOptions)
        }.Authorized(memberToken, team.Id));
        var leave = await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);

        var rejectResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/leave-requests/{leave!.Id}/reject")
        {
            Content = JsonContent.Create(new DecideLeaveRequestDto("Short-staffed that week"), options: JsonOptions)
        }.Authorized(adminToken, team.Id));
        rejectResponse.EnsureSuccessStatusCode();
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);
        Assert.Equal(Models.LeaveStatus.Rejected, rejected!.Status);

        var secondApprove = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/leave-requests/{leave.Id}/approve").Authorized(adminToken, team.Id));
        Assert.Equal(HttpStatusCode.BadRequest, secondApprove.StatusCode);
    }

    [Fact]
    public async Task Leave_requests_auto_approve_by_default()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("leave3-admin@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Leave Team 3");
        await client.InviteMemberAsync(adminToken, team.Id, "leave3-member@test.local", "Viewer", "EMP-002");
        var memberToken = await client.RegisterAndLoginAsync("leave3-member@test.local");

        var createResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/leave-requests")
        {
            Content = JsonContent.Create(new CreateLeaveRequestDto(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), null), options: JsonOptions)
        }.Authorized(memberToken, team.Id));
        createResponse.EnsureSuccessStatusCode();
        var leave = await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);

        Assert.Equal(Models.LeaveStatus.Approved, leave!.Status);
        Assert.NotNull(leave.DecidedAt);
    }

    private sealed record RosterResponseShape(List<LeaveRequestDto> LeaveRequests);
}
