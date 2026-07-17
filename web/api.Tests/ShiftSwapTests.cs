using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class ShiftSwapTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ShiftSwapTests(TestWebApplicationFactory factory) => _factory = factory;

    private static async Task CreateWorkShiftTypeAsync(HttpClient client, string token, int teamId, string code = "M")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/shift-types")
        {
            Content = JsonContent.Create(new ShiftTypeDto(null, code, "Morning", null, null, "#A8701F", false, true), options: JsonOptions)
        }.Authorized(token, teamId);
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    private static async Task SetShiftAsync(HttpClient client, string token, int teamId, int teamMemberId, DateOnly date, string? code)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/roster/entry")
        {
            Content = JsonContent.Create(new RosterEntryUpsertDto(teamMemberId, date, code, null), options: JsonOptions)
        }.Authorized(token, teamId);
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Offer_claim_and_approve_moves_the_shift_from_offerer_to_claimant()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("swap1-admin@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Swap Team 1");
        await client.SetAutoApproveAsync(adminToken, team.Id, autoApproveLeaveRequests: false, autoApproveShiftSwaps: false);
        var date = new DateOnly(2026, 8, 3);
        await CreateWorkShiftTypeAsync(client, adminToken, team.Id);

        await client.InviteMemberAsync(adminToken, team.Id, "swap1-a@test.local", "Viewer", "EMP-002");
        var aToken = await client.RegisterAndLoginAsync("swap1-a@test.local");
        var aMember = (await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members/me").Authorized(aToken, team.Id)))
            .Content.ReadFromJsonAsync<MeDto>(JsonOptions))!;

        await client.InviteMemberAsync(adminToken, team.Id, "swap1-b@test.local", "Viewer", "EMP-003");
        var bToken = await client.RegisterAndLoginAsync("swap1-b@test.local");

        // Resolve numeric TeamMember ids via the members list (MeDto only carries PersonId).
        var members = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, team.Id)))
            .Content.ReadFromJsonAsync<List<TeamMemberDto>>(JsonOptions);
        var memberA = members!.Single(m => m.PersonId == aMember.PersonId);
        var memberB = members!.Single(m => m.Code == "EMP-003");

        await SetShiftAsync(client, adminToken, team.Id, memberA.Id, date, "M");

        var offerResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/shift-swaps")
        {
            Content = JsonContent.Create(new CreateShiftSwapDto(date, "M", null), options: JsonOptions)
        }.Authorized(aToken, team.Id));
        offerResponse.EnsureSuccessStatusCode();
        var offer = await offerResponse.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(JsonOptions);
        Assert.Equal(Models.SwapStatus.Open, offer!.Status);

        // The offerer can't claim their own offer.
        var selfClaim = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/shift-swaps/{offer.Id}/claim").Authorized(aToken, team.Id));
        Assert.Equal(HttpStatusCode.BadRequest, selfClaim.StatusCode);

        var claimResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/shift-swaps/{offer.Id}/claim").Authorized(bToken, team.Id));
        claimResponse.EnsureSuccessStatusCode();
        var claimed = await claimResponse.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(JsonOptions);
        Assert.Equal(Models.SwapStatus.Claimed, claimed!.Status);
        Assert.Equal(memberB.Id, claimed.ClaimedByTeamMemberId);

        var approveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/shift-swaps/{offer.Id}/approve").Authorized(adminToken, team.Id));
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(JsonOptions);
        Assert.Equal(Models.SwapStatus.Approved, approved!.Status);

        var roster = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=8").Authorized(adminToken, team.Id)))
            .Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);

        var aEntry = roster!.Entries.SingleOrDefault(e => e.TeamMemberId == memberA.Id && e.Date == date);
        var bEntry = roster.Entries.SingleOrDefault(e => e.TeamMemberId == memberB.Id && e.Date == date);
        Assert.True(aEntry is null || aEntry.ShiftCode is null);
        Assert.Equal("M", bEntry?.ShiftCode);
    }

    [Fact]
    public async Task A_swap_targeted_at_one_member_cannot_be_claimed_by_someone_else()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("swap2-admin@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Swap Team 2");
        var date = new DateOnly(2026, 9, 5);
        await CreateWorkShiftTypeAsync(client, adminToken, team.Id);

        await client.InviteMemberAsync(adminToken, team.Id, "swap2-a@test.local", "Viewer", "EMP-002");
        var aToken = await client.RegisterAndLoginAsync("swap2-a@test.local");
        await client.InviteMemberAsync(adminToken, team.Id, "swap2-b@test.local", "Viewer", "EMP-003");
        var bToken = await client.RegisterAndLoginAsync("swap2-b@test.local");
        await client.InviteMemberAsync(adminToken, team.Id, "swap2-c@test.local", "Viewer", "EMP-004");
        var cToken = await client.RegisterAndLoginAsync("swap2-c@test.local");

        var members = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, team.Id)))
            .Content.ReadFromJsonAsync<List<TeamMemberDto>>(JsonOptions);
        var memberA = members!.Single(m => m.Code == "EMP-002");
        var memberC = members!.Single(m => m.Code == "EMP-004");

        await SetShiftAsync(client, adminToken, team.Id, memberA.Id, date, "M");

        var offerResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/shift-swaps")
        {
            Content = JsonContent.Create(new CreateShiftSwapDto(date, "M", memberC.Id), options: JsonOptions)
        }.Authorized(aToken, team.Id));
        var offer = await offerResponse.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(JsonOptions);

        var wrongClaim = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/shift-swaps/{offer!.Id}/claim").Authorized(bToken, team.Id));
        Assert.Equal(HttpStatusCode.Forbidden, wrongClaim.StatusCode);

        var rightClaim = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/shift-swaps/{offer.Id}/claim").Authorized(cToken, team.Id));
        rightClaim.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Claiming_a_swap_auto_approves_and_moves_the_shift_by_default()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("swap3-admin@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Swap Team 3");
        var date = new DateOnly(2026, 10, 6);
        await CreateWorkShiftTypeAsync(client, adminToken, team.Id);

        await client.InviteMemberAsync(adminToken, team.Id, "swap3-a@test.local", "Viewer", "EMP-002");
        var aToken = await client.RegisterAndLoginAsync("swap3-a@test.local");
        await client.InviteMemberAsync(adminToken, team.Id, "swap3-b@test.local", "Viewer", "EMP-003");
        var bToken = await client.RegisterAndLoginAsync("swap3-b@test.local");

        var members = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/members").Authorized(adminToken, team.Id)))
            .Content.ReadFromJsonAsync<List<TeamMemberDto>>(JsonOptions);
        var memberA = members!.Single(m => m.Code == "EMP-002");
        var memberB = members!.Single(m => m.Code == "EMP-003");

        await SetShiftAsync(client, adminToken, team.Id, memberA.Id, date, "M");

        var offerResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/shift-swaps")
        {
            Content = JsonContent.Create(new CreateShiftSwapDto(date, "M", null), options: JsonOptions)
        }.Authorized(aToken, team.Id));
        var offer = await offerResponse.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(JsonOptions);

        var claimResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/shift-swaps/{offer!.Id}/claim").Authorized(bToken, team.Id));
        claimResponse.EnsureSuccessStatusCode();
        var claimed = await claimResponse.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(JsonOptions);

        // No separate /approve call — claiming alone should have already settled it.
        Assert.Equal(Models.SwapStatus.Approved, claimed!.Status);

        var roster = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/roster?year=2026&month=10").Authorized(adminToken, team.Id)))
            .Content.ReadFromJsonAsync<RosterResponseShape>(JsonOptions);

        var aEntry = roster!.Entries.SingleOrDefault(e => e.TeamMemberId == memberA.Id && e.Date == date);
        var bEntry = roster.Entries.SingleOrDefault(e => e.TeamMemberId == memberB.Id && e.Date == date);
        Assert.True(aEntry is null || aEntry.ShiftCode is null);
        Assert.Equal("M", bEntry?.ShiftCode);
    }

    private sealed record RosterResponseShape(List<RosterEntryShape> Entries);
    private sealed record RosterEntryShape(int TeamMemberId, DateOnly Date, string? ShiftCode);
}
