using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.DomainDrivenDesign.Interfaces;

namespace Monica.DomainDrivenDesign;

public abstract class MoDomainService : IMoDomainService
{
}

public abstract class MoDomainService<TSelf> : MoDomainService where TSelf : MoDomainService<TSelf>
{
    protected ILogger<TSelf> _logger { get; } = LogManager.For<TSelf>();
}
