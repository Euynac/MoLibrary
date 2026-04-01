namespace Monica.DependencyInjection.Models.Internal;

/// <summary>
/// Identifies a service registration by service type and optional service key.
/// </summary>
/// <remarks>
/// This mirrors the shape used internally by the Microsoft dependency-injection implementation,
/// but remains a Monica-internal model.
/// </remarks>
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
