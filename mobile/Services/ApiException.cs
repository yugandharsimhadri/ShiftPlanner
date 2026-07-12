namespace ShiftPlanner.Mobile.Services;

/// <summary>A user-presentable error raised by <see cref="ApiClient"/>.</summary>
public sealed class ApiException : Exception
{
    public int? StatusCode { get; }

    public ApiException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
