using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Services;

// Resolved once per request by RequireTeamFilter and stashed on HttpContext.Items.
// Every tenant-scoped endpoint reads TeamId from here — never from a client-supplied
// value — so a request can never operate on a team the caller isn't a member of.
public sealed class TeamContext
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required int TeamId { get; init; }
    public required TeamRole Role { get; init; }
}

public static class TeamContextHttpExtensions
{
    private const string ItemsKey = "ShiftPlanner.TeamContext";

    public static void SetTeamContext(this HttpContext ctx, TeamContext teamContext) =>
        ctx.Items[ItemsKey] = teamContext;

    public static TeamContext GetTeamContext(this HttpContext ctx) =>
        (TeamContext)ctx.Items[ItemsKey]!;
}
