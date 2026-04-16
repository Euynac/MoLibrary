# Configuration Template

Use `$DomainNamespace$` for the domain project namespace selected by the architecture skill, such as `OrderingService.Domain` or `Domains.Ordering`.

## Use When

- A feature needs host-provided runtime configuration.
- Consumers should receive typed options through dependency injection.

## Rules

- Mark the class with `[Configuration]`.
- End the class name with `Options`.
- Use `IOptions<T>` for mostly static configuration.
- Use `IOptionsSnapshot<T>` for per-scope refreshed values.
- Use `IOptionsMonitor<T>` for long-lived services that react to changes.

## Options Class Example

```csharp
using Monica.Configuration.Annotations;

namespace $DomainNamespace$.Configurations;

[Configuration]
public sealed class OrderProcessingOptions
{
    public bool AutoApproveEnabled { get; set; }

    public int ApprovalBatchSize { get; set; } = 100;
}
```

## Consumer Example

```csharp
using Microsoft.Extensions.Options;
using Monica.WebApi.Abstractions;

namespace $DomainNamespace$.DomainServices;

public sealed class DomainOrderApproval(
    IOptions<OrderProcessingOptions> options)
    : DomainService
{
    private OrderProcessingOptions Options => options.Value;

    public bool IsAutoApproveEnabled()
    {
        return Options.AutoApproveEnabled;
    }
}
```

## Notes

- Use configuration for environment- or host-specific behavior, not for domain constants that belong in code.
- Keep option names explicit and developer-facing.
