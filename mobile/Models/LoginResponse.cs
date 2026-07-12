using System.Text.Json.Serialization;

namespace ShiftPlanner.Mobile.Models;

/// <summary>
/// Response from POST /login. Matches ASP.NET Core Identity's MapIdentityApi
/// bearer-token shape: { "tokenType": "Bearer", "accessToken": "...", "expiresIn": 3600, "refreshToken": "..." }.
/// </summary>
public sealed class LoginResponse
{
    [JsonPropertyName("tokenType")]
    public string? TokenType { get; set; }

    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public double ExpiresIn { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
}
