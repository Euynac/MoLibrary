using System.Collections.Frozen;
using Monica.StateStore.Abstractions;

namespace Monica.StateStore.Services;

internal sealed class StateDocumentProfileProvider(IEnumerable<StateDocumentProfile> profiles)
    : IStateDocumentProfileProvider
{
    private readonly FrozenDictionary<string, StateDocumentProfile> _profiles = profiles
        .ToFrozenDictionary(static profile => profile.Name, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, StateDocumentProfile> Profiles => _profiles;

    public StateDocumentProfile GetRequiredProfile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _profiles.TryGetValue(name, out var profile)
            ? profile
            : throw new KeyNotFoundException(
                $"State document profile '{name}' is not defined. Available profiles: " +
                string.Join(", ", _profiles.Keys.Order(StringComparer.Ordinal)) + ".");
    }
}
