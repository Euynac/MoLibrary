namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Coordinates store-wide operations that require serialization across configuration-store clients.
/// </summary>
internal sealed class ConfigurationStoreLockEntity
{
    internal const string DefinitionPublicationLockKey = "DefinitionPublication";
    internal const string MutationGroupsLockKey = "MutationGroups";
    internal const string UnifiedVersionsLockKey = "UnifiedVersions";

    /// <summary>
    /// Gets or sets the stable name of the serialized store operation.
    /// </summary>
    public required string LockKey { get; set; }

    /// <summary>
    /// Gets or sets the value incremented to acquire the database row lock within the caller's transaction.
    /// </summary>
    public long LockVersion { get; set; }

    internal static ConfigurationStoreLockEntity[] CreateSeedRows()
    {
        return
        [
            new() { LockKey = DefinitionPublicationLockKey },
            new() { LockKey = MutationGroupsLockKey },
            new() { LockKey = UnifiedVersionsLockKey }
        ];
    }
}
