using Dapr.Client;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Providers;
using Monica.Modules;

namespace Monica.Configuration.Dapr.Notifications;

/// <summary>
/// Subscribes to Dapr Configuration API streaming updates for known Monica configuration keys.
/// </summary>
public sealed class DaprConfigurationChangeSubscriber(
    DaprClient daprClient,
    IConfigurationDefinitionRegistry definitionRegistry,
    IOptions<ModuleConfigurationDaprOption> options)
    : IConfigurationChangeSubscriber
{
    private readonly ModuleConfigurationDaprOption _option = options.Value;

    /// <inheritdoc />
    public async Task SubscribeAsync(
        Func<ConfigurationChangeNotification, CancellationToken, Task> onNotification,
        CancellationToken cancellationToken)
    {
        var leavesByKey = definitionRegistry.GetAll()
            .SelectMany(definition => ConfigurationSourceNodeEnumerator
                .EnumerateLeaves(definition)
                .Select(leaf => new { definition, leaf }))
            .ToDictionary(item => item.leaf.ConfigurationPath, item => item, StringComparer.OrdinalIgnoreCase);
        var keys = leavesByKey.Keys.ToArray();

        if (keys.Length == 0)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return;
        }

        var response = await daprClient.SubscribeConfiguration(
            _option.StoreName,
            keys,
            _option.Metadata,
            cancellationToken);

        try
        {
            await foreach (var items in response.Source.WithCancellation(cancellationToken))
            {
                foreach (var item in items)
                {
                    if (!leavesByKey.TryGetValue(item.Key, out var leaf))
                    {
                        continue;
                    }

                    await onNotification(new ConfigurationChangeNotification
                    {
                        NotificationId = Guid.NewGuid().ToString("N"),
                        DefinitionKey = leaf.definition.DefinitionKey,
                        LogicalPath = leaf.leaf.LogicalPath,
                        SourceKey = "dapr:default",
                        Version = long.TryParse(item.Value.Version, out var version) ? version : null,
                        ChangedTime = DateTimeOffset.UtcNow
                    }, cancellationToken);
                }
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(response.Id))
            {
                await daprClient.UnsubscribeConfiguration(_option.StoreName, response.Id, CancellationToken.None);
            }
        }
    }
}
