using Microsoft.Extensions.Http;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Resolves domain dependency flags and the target application identifier for each dependent RPC domain.
/// </summary>
public interface IRpcClientDomainInfoProvider
{
    object GetDependencyDomains();

    string GetDomainRelatedAppId(Enum domain);
}
