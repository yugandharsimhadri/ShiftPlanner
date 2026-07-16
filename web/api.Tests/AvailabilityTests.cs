using System.Net;
using System.Net.Http.Json;
using ShiftPlanner.Api.Dtos;
using Xunit;
using static ShiftPlanner.Api.Tests.ApiTestClient;

namespace ShiftPlanner.Api.Tests;

public class AvailabilityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AvailabilityTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Toggling_yourself_available_shows_up_on_the_team_dashboard()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("avail-admin1@test.local");
        var team = await client.CreateTeamAsync(token, "Avail Team 1");

        var toggleOnRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/teams/current/members/me/availability")
        {
            Content = JsonContent.Create(new UpdateAvailabilityDto(true), options: JsonOptions)
        }.Authorized(token, team.Id);
        var toggleOnResponse = await client.SendAsync(toggleOnRequest);
        toggleOnResponse.EnsureSuccessStatusCode();
        var toggled = await toggleOnResponse.Content.ReadFromJsonAsync<TeamMemberAvailabilityDto>(JsonOptions);
        Assert.True(toggled!.IsAvailable);
        Assert.NotNull(toggled.AvailableSince);

        var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/availability").Authorized(token, team.Id);
        var dashboardResponse = await client.SendAsync(dashboardRequest);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<List<TeamMemberAvailabilityDto>>(JsonOptions);

        Assert.Contains(dashboard!, m => m.TeamMemberId == toggled.TeamMemberId && m.IsAvailable);
    }

    [Fact]
    public async Task Toggling_off_clears_availability()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("avail-admin2@test.local");
        var team = await client.CreateTeamAsync(token, "Avail Team 2");

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/teams/current/members/me/availability")
        {
            Content = JsonContent.Create(new UpdateAvailabilityDto(true), options: JsonOptions)
        }.Authorized(token, team.Id));

        var offResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/teams/current/members/me/availability")
        {
            Content = JsonContent.Create(new UpdateAvailabilityDto(false), options: JsonOptions)
        }.Authorized(token, team.Id));
        var off = await offResponse.Content.ReadFromJsonAsync<TeamMemberAvailabilityDto>(JsonOptions);

        Assert.False(off!.IsAvailable);
        Assert.Null(off.AvailableSince);
    }

    [Fact]
    public async Task A_Viewer_role_team_member_can_see_the_dashboard_without_admin_rights()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.RegisterAndLoginAsync("avail-admin3@test.local");
        var team = await client.CreateTeamAsync(adminToken, "Avail Team 3");

        var createResponse = await client.CreateTeamMemberAsync(
            adminToken, team.Id, "EMP-002", "Viewer Person", trackId: null, accessRole: "Viewer", email: "avail-viewer3@test.local");
        createResponse.EnsureSuccessStatusCode();

        var viewerToken = await client.RegisterAndLoginAsync("avail-viewer3@test.local");

        var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/api/teams/current/availability").Authorized(viewerToken, team.Id);
        var dashboardResponse = await client.SendAsync(dashboardRequest);

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
    }

    [Fact]
    public async Task Profile_defaults_expiry_hours_from_timezone_and_lets_the_user_override_it()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("avail-admin4@test.local");
        await client.CreateTeamAsync(token, "Avail Team 4"); // creates the Person row

        var getResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/me/profile").Authorized(token));
        var profile = await getResponse.Content.ReadFromJsonAsync<ProfileDto>(JsonOptions);
        Assert.Equal(8, profile!.AutoExpiryHours); // no timezone set yet -> "others" default
        Assert.Null(profile.AutoExpiryHoursOverride);

        var patchResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/me/profile")
        {
            Content = JsonContent.Create(new UpdateProfileDto("Asia/Kolkata", null), options: JsonOptions)
        }.Authorized(token));
        var updated = await patchResponse.Content.ReadFromJsonAsync<ProfileDto>(JsonOptions);
        Assert.Equal(9, updated!.AutoExpiryHours); // now Asia/Kolkata default

        var overrideResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/me/profile")
        {
            Content = JsonContent.Create(new UpdateProfileDto(null, 2), options: JsonOptions)
        }.Authorized(token));
        var overridden = await overrideResponse.Content.ReadFromJsonAsync<ProfileDto>(JsonOptions);
        Assert.Equal(2, overridden!.AutoExpiryHours);
        Assert.Equal(2, overridden.AutoExpiryHoursOverride);
    }
}
