namespace Monica.Configuration.Exceptions;

/// <summary>
/// Represents a configuration-store operation that failed because its physical database schema is missing or incompatible.
/// </summary>
/// <remarks>
/// Monica.Configuration does not create or update database schemas at runtime. The host must apply the EF Core migrations
/// that own its configuration store before the store is used.
/// </remarks>
public sealed class ConfigurationStoreSchemaException : InvalidOperationException
{
    /// <summary>
    /// Initializes a configuration-store schema failure.
    /// </summary>
    /// <param name="message">The operator-facing message that explains how to prepare the store schema.</param>
    /// <param name="innerException">The database-provider exception that identified the missing schema object.</param>
    public ConfigurationStoreSchemaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
