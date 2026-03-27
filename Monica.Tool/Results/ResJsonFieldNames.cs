namespace Monica.Tool.Results;

/// <summary>
/// Defines the stable JSON field names used by Monica internal result envelopes.
/// </summary>
public static class ResJsonFieldNames
{
    public const string Message = "message";
    public const string Status = "code";
    public const string Metadata = "extraInfo";
    public const string Data = "data";
}
