namespace Monica.Core.HostedService.Models.Internal;

internal sealed record HostedServiceCheckpointKey(Type ServiceType, string Checkpoint);
