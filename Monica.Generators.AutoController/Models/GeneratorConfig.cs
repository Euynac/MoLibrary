using System;

namespace Monica.Generators.AutoController.Models;

internal sealed class GeneratorConfig : IEquatable<GeneratorConfig>
{
    public GeneratorConfig(
        string routePrefix,
        string? domainName,
        int rpcClientTargets,
        string? httpClientBaseTypeName,
        string? localClientBaseTypeName)
    {
        RoutePrefix = routePrefix;
        DomainName = domainName;
        RpcClientTargets = rpcClientTargets;
        HttpClientBaseTypeName = httpClientBaseTypeName;
        LocalClientBaseTypeName = localClientBaseTypeName;
    }

    public string RoutePrefix { get; }

    public string? DomainName { get; }

    public int RpcClientTargets { get; }

    public string? HttpClientBaseTypeName { get; }

    public string? LocalClientBaseTypeName { get; }

    public bool GenerateHttpClient => (RpcClientTargets & 1) != 0;

    public bool GenerateLocalClient => (RpcClientTargets & 2) != 0;

    public bool Equals(GeneratorConfig? other)
    {
        return other is not null &&
               RoutePrefix == other.RoutePrefix &&
               DomainName == other.DomainName &&
               RpcClientTargets == other.RpcClientTargets &&
               HttpClientBaseTypeName == other.HttpClientBaseTypeName &&
               LocalClientBaseTypeName == other.LocalClientBaseTypeName;
    }

    public override bool Equals(object? obj)
    {
        return obj is GeneratorConfig other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(RoutePrefix);
            hashCode = (hashCode * 397) ^ (DomainName is null ? 0 : StringComparer.Ordinal.GetHashCode(DomainName));
            hashCode = (hashCode * 397) ^ RpcClientTargets;
            hashCode = (hashCode * 397) ^ (HttpClientBaseTypeName is null ? 0 : StringComparer.Ordinal.GetHashCode(HttpClientBaseTypeName));
            hashCode = (hashCode * 397) ^ (LocalClientBaseTypeName is null ? 0 : StringComparer.Ordinal.GetHashCode(LocalClientBaseTypeName));
            return hashCode;
        }
    }
}
