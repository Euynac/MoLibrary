namespace Monica.Core.Localization.Models;

internal sealed class LocalizationResourceRegistry
{
    private readonly object _syncRoot = new();
    private LocalizationResourceRegistration[] _registrations = [];
    private Dictionary<Type, LocalizationResourceRegistration> _registrationLookup = [];

    public void ReplaceFromTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var registrations = types
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                           typeof(IMoLocalizationResource).IsAssignableFrom(type))
            .Distinct()
            .Select(LocalizationResourceRegistration.Create)
            .OrderBy(registration => registration.ResourceType.FullName, StringComparer.Ordinal)
            .ToArray();

        lock (_syncRoot)
        {
            _registrations = registrations;
            _registrationLookup = registrations.ToDictionary(
                static registration => registration.ResourceType,
                static registration => registration);
        }
    }

    public IReadOnlyList<LocalizationResourceRegistration> GetRegistrations()
    {
        lock (_syncRoot)
        {
            return _registrations.ToArray();
        }
    }

    public bool TryGetRegistration(Type resourceType, out LocalizationResourceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        lock (_syncRoot)
        {
            return _registrationLookup.TryGetValue(resourceType, out registration!);
        }
    }
}
