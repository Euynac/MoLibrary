using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions.Internal;

internal interface IConfigurationUnifiedVersionCoordinator
{
    Task CaptureStandaloneMutationAsync(ConfigurationValueHistory history, CancellationToken cancellationToken);

    Task CaptureMutationGroupAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken);
}
