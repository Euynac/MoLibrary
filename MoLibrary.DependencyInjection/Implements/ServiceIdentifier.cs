namespace MoLibrary.DependencyInjection.Implements;

/// <summary>
/// https://github.com/dotnet/runtime/blob/release/8.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ServiceIdentifier.cs#L9
/// </summary>
public readonly struct ServiceIdentifier(object? serviceKey, Type serviceType) : IEquatable<ServiceIdentifier>
{
    public object? ServiceKey { get; } = serviceKey;

    public Type ServiceType { get; } = serviceType;

    public ServiceIdentifier(Type serviceType) : this(null, serviceType)
    {
    }

    public bool Equals(ServiceIdentifier other)
    {
        if (ServiceKey == null && other.ServiceKey == null)
        {
            return ServiceType == other.ServiceType;
        }
        else if (ServiceKey != null && other.ServiceKey != null)
        {
            return ServiceType == other.ServiceType && ServiceKey.Equals(other.ServiceKey);
        }
        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is ServiceIdentifier identifier && Equals(identifier);
    }

    public override int GetHashCode()
    {
        if (ServiceKey == null)
        {
            return ServiceType.GetHashCode();
        }
        unchecked
        {
            return ServiceType.GetHashCode() * 397 ^ ServiceKey.GetHashCode();
        }
    }

    public static bool operator ==(ServiceIdentifier left, ServiceIdentifier right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ServiceIdentifier left, ServiceIdentifier right)
    {
        return !(left == right);
    }
}
