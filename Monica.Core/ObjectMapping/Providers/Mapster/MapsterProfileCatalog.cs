using Mapster;
using Monica.Core.TypeDiscovery.Abstractions;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

/// <summary>
/// Collects and activates the Mapster profiles owned by one Monica host.
/// </summary>
internal sealed class MapsterProfileCatalog
{
    private readonly Dictionary<string, Type> _discoveredProfiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Records a concrete Mapster profile discovered through Monica's business-type pipeline.
    /// </summary>
    /// <param name="type">The current business type.</param>
    public void Discover(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!IsDiscoverableProfile(type))
        {
            return;
        }

        _discoveredProfiles.TryAdd(GetProfileKey(type), type);
    }

    /// <summary>
    /// Applies explicit profiles first, followed by automatically discovered profiles in dependency-first order.
    /// </summary>
    /// <param name="config">The host-owned Mapster configuration.</param>
    /// <param name="explicitProfileTypes">Profiles explicitly registered through module registration extensions.</param>
    /// <param name="typeDependencyOrderer">The host-bound orderer for discovered business types.</param>
    public void ApplyProfiles(
        TypeAdapterConfig config,
        IEnumerable<Type> explicitProfileTypes,
        ITypeDependencyOrderer typeDependencyOrderer)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(explicitProfileTypes);
        ArgumentNullException.ThrowIfNull(typeDependencyOrderer);

        foreach (var profileType in GetOrderedProfileTypes(explicitProfileTypes, typeDependencyOrderer))
        {
            CreateProfile(profileType).Register(config);
        }
    }

    /// <summary>
    /// Creates the final profile sequence for the current host.
    /// </summary>
    internal IReadOnlyList<Type> GetOrderedProfileTypes(
        IEnumerable<Type> explicitProfileTypes,
        ITypeDependencyOrderer typeDependencyOrderer)
    {
        ArgumentNullException.ThrowIfNull(explicitProfileTypes);
        ArgumentNullException.ThrowIfNull(typeDependencyOrderer);

        var orderedProfiles = new List<Type>();
        var addedProfileKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profileType in explicitProfileTypes)
        {
            AddOnce(profileType, orderedProfiles, addedProfileKeys);
        }

        foreach (var profileType in typeDependencyOrderer.OrderDependencyFirst(_discoveredProfiles.Values))
        {
            AddOnce(profileType, orderedProfiles, addedProfileKeys);
        }

        return orderedProfiles;
    }

    private static bool IsDiscoverableProfile(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
               typeof(IRegister).IsAssignableFrom(type);
    }

    private static IRegister CreateProfile(Type profileType)
    {
        if (!IsDiscoverableProfile(profileType))
        {
            throw CreateInvalidProfileException(profileType);
        }

        try
        {
            return Activator.CreateInstance(profileType, nonPublic: true) as IRegister
                   ?? throw CreateInvalidProfileException(profileType);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Object-mapping profile '{profileType.FullName}' must have a parameterless constructor that Monica can activate.",
                exception);
        }
    }

    private static InvalidOperationException CreateInvalidProfileException(Type profileType)
    {
        return new InvalidOperationException(
            $"Object-mapping profile '{profileType.FullName}' must be a concrete, closed {nameof(IRegister)} type with a parameterless constructor.");
    }

    private static void AddOnce(
        Type profileType,
        ICollection<Type> orderedProfiles,
        ISet<string> addedProfileKeys)
    {
        if (addedProfileKeys.Add(GetProfileKey(profileType)))
        {
            orderedProfiles.Add(profileType);
        }
    }

    private static string GetProfileKey(Type profileType)
    {
        return profileType.AssemblyQualifiedName
               ?? throw new InvalidOperationException(
                   $"Object-mapping profile '{profileType.FullName}' does not have an assembly-qualified type name.");
    }
}
