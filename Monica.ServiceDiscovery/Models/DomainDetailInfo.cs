namespace Monica.ServiceDiscovery.Models;

public sealed class DomainDetailInfo
{
    public required DomainInfo Domain { get; init; }

    public List<RegisteredServiceStatus> RelatedServices { get; init; } = [];
}
