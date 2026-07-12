namespace ShiftPlanner.Mobile.Services;

/// <summary>
/// Wraps MAUI's <see cref="SecureStorage"/> (Android Keystore / Windows DPAPI backed) to
/// hold the bearer token returned by POST /login. Nothing in here is ever written with
/// <see cref="Preferences"/>, which is not encrypted.
/// </summary>
public static class SecureTokenStore
{
    private const string AccessTokenKey = "shiftplanner.access_token";
    private const string TokenExpiryKey = "shiftplanner.token_expiry_utc";

    public static async Task SaveTokenAsync(string accessToken, DateTimeOffset expiresAtUtc)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(TokenExpiryKey, expiresAtUtc.ToString("O"));
    }

    public static Task<string?> GetTokenAsync() => SecureStorage.Default.GetAsync(AccessTokenKey);

    /// <summary>True if a token exists and has not passed its expiry.</summary>
    public static async Task<bool> HasValidTokenAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var expiryRaw = await SecureStorage.Default.GetAsync(TokenExpiryKey);
        if (DateTimeOffset.TryParse(expiryRaw, out var expiry) && expiry <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        return true;
    }

    public static void ClearToken() => SecureStorage.Default.RemoveAll();
}
