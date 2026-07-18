using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores.Support;

/// <summary>
/// Owns conversion between a service publication and its persisted current publisher state.
/// </summary>
internal sealed record PublishedDefinitionPublisherStateCandidate
{
    internal required string DefinitionIdentity { get; init; }

    internal required string PublisherIdentity { get; init; }

    internal required string PublisherKey { get; init; }

    internal required ConfigurationReloadBehaviorObservation Observation { get; init; }

    internal static PublishedDefinitionPublisherStateCandidate FromPublication(
        ConfigurationPublisherIdentity publisher,
        ConfigurationDefinitionPublication publication)
    {
        return new PublishedDefinitionPublisherStateCandidate
        {
            DefinitionIdentity = ConfigurationDefinitionIdentity.Compute(publication.Definition.DefinitionKey),
            PublisherIdentity = PublishedDefinitionPublisherIdentity.Compute(publisher.PublisherKey),
            PublisherKey = publisher.PublisherKey,
            Observation = publication.ReloadBehaviorObservation
        };
    }

    internal ConfigurationDefinitionPublisherStateEntity CreateEntity()
    {
        var entity = new ConfigurationDefinitionPublisherStateEntity
        {
            DefinitionIdentity = DefinitionIdentity,
            PublisherIdentity = PublisherIdentity
        };
        ApplyTo(entity);
        return entity;
    }

    internal void ApplyTo(ConfigurationDefinitionPublisherStateEntity entity)
    {
        entity.PublisherKey = PublisherKey;
        entity.ObservationKind = Observation.Kind.ToString();
        entity.ReloadBehavior = Observation.Behavior.ToString();
    }

    internal static ConfigurationReloadBehaviorObservation MaterializeObservation(
        ConfigurationDefinitionPublisherStateEntity entity)
    {
        var state = Materialize(entity);
        return new ConfigurationReloadBehaviorObservation(state.ObservationKind, state.ReloadBehavior);
    }

    internal static ConfigurationDefinitionPublisherState Materialize(
        ConfigurationDefinitionPublisherStateEntity entity)
    {
        if (!Enum.TryParse<ConfigurationReloadBehaviorObservationKind>(
                entity.ObservationKind,
                ignoreCase: false,
                out var kind)
            || !Enum.IsDefined(kind))
        {
            throw new InvalidDataException(
                $"Published definition publisher state uses unsupported observation kind '{entity.ObservationKind}'.");
        }

        if (!Enum.TryParse<ConfigurationReloadBehavior>(
                entity.ReloadBehavior,
                ignoreCase: false,
                out var behavior)
            || !Enum.IsDefined(behavior))
        {
            throw new InvalidDataException(
                $"Published definition publisher state uses unsupported reload behavior '{entity.ReloadBehavior}'.");
        }

        _ = new ConfigurationReloadBehaviorObservation(kind, behavior);
        return new ConfigurationDefinitionPublisherState
        {
            PublisherKey = entity.PublisherKey,
            ObservationKind = kind,
            ReloadBehavior = behavior
        };
    }
}
