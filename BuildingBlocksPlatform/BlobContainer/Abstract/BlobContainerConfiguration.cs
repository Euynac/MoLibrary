using BuildingBlocksPlatform.Extensions;
using JetBrains.Annotations;
using MoLibrary.Tool.Utils;


namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobContainerConfiguration
{
    private readonly BlobContainerConfiguration? _fallbackConfiguration;

    private readonly Dictionary<string, object?> _properties;

    public BlobContainerConfiguration(BlobContainerConfiguration? fallbackConfiguration = null)
    {
        NamingNormalizers = new TypeList<IBlobNamingNormalizer>();
        _fallbackConfiguration = fallbackConfiguration;
        _properties = new Dictionary<string, object?>();
    }

    /// <summary>
    ///     The provider to be used to store BLOBs of this container.
    /// </summary>
    public Type? ProviderType { get; set; }

    /// <summary>
    ///     Indicates whether this container is multi-tenant or not.
    ///     If this is <code>false</code> and your application is multi-tenant,
    ///     then the container is shared by all tenants in the system.
    ///     This can be <code>true</code> even if your application is not multi-tenant.
    ///     Default: true.
    /// </summary>
    public bool IsMultiTenant { get; set; } = true;

    public ITypeList<IBlobNamingNormalizer> NamingNormalizers { get; }

    public T? GetConfigurationOrDefault<T>(string name, T? defaultValue = default)
    {
        return (T?) GetConfigurationOrNull(name, defaultValue);
    }

    public object? GetConfigurationOrNull(string name, object? defaultValue = null)
    {
        return _properties.GetOrDefault(name) ??
               _fallbackConfiguration?.GetConfigurationOrNull(name, defaultValue) ??
               defaultValue;
    }

    public BlobContainerConfiguration SetConfiguration(string name, object? value)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.NotNull(value, nameof(value));

        _properties[name] = value;

        return this;
    }

    public BlobContainerConfiguration ClearConfiguration(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        _properties.Remove(name);

        return this;
    }
}