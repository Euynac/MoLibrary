namespace Monica.Core.Results;

/// <summary>
/// Provides global access to the configured Monica result projector.
/// </summary>
public static class ResultEnvelopeProvider
{
    /// <summary>
    /// Gets the configured result projector. Set during module registration.
    /// </summary>
    public static IResultProjector? Projector { get; internal set; }

    /// <summary>
    /// Returns the projected payload when a projector is configured; otherwise returns the original envelope.
    /// </summary>
    /// <param name="response">The Monica result envelope.</param>
    /// <returns>The payload that should be serialized in the HTTP response.</returns>
    public static object GetResponsePayload(IResultEnvelope response)
    {
        return Projector?.Project(response) ?? response;
    }
}
