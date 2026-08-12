namespace Monica.StateStore.Abstractions;

/// <summary>
/// Resolves immutable state-document profiles compiled for one host.
/// </summary>
public interface IStateDocumentProfileProvider
{
    /// <summary>
    /// Gets all profiles keyed by their case-sensitive logical names.
    /// </summary>
    IReadOnlyDictionary<string, StateDocumentProfile> Profiles { get; }

    /// <summary>
    /// Gets a required profile or throws an actionable exception when the name is unknown.
    /// </summary>
    /// <param name="name">The case-sensitive logical profile name.</param>
    StateDocumentProfile GetRequiredProfile(string name);
}
