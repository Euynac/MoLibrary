using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Handlers;
using MoLibrary.EventBus.Abstractions.Subscriptions;

namespace MoLibrary.EventBus.Models;

/// <summary>
/// Concrete subscription implementation with lifecycle management.
/// </summary>
internal class Subscription(SubscriptionDescriptor descriptor) : ISubscription
{
    private SubscriptionState _state = SubscriptionState.Pending;
    private DateTimeOffset? _activatedAt;
    private DateTimeOffset? _deactivatedAt;

    #region Identity

    public SubscriptionId Id { get; } = SubscriptionId.NewId();
    public string? ServiceKey { get; } = descriptor.ServiceKey;
    public Type EventType { get; } = descriptor.EventType;
    public string TopicName { get; } = descriptor.TopicName;

    #endregion

    #region Handler Information

    public Type? HandlerType { get; } = descriptor.HandlerType;
    public IEventHandlerFactory HandlerFactory { get; } = descriptor.HandlerFactory;

    #endregion

    #region Scope Information

    public SubscriptionScope Scope { get; } = descriptor.Scope;

    #endregion

    #region Lifecycle

    public SubscriptionState State => _state;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt => _activatedAt;
    public DateTimeOffset? DeactivatedAt => _deactivatedAt;
    public bool IsAutoDiscovered { get; } = descriptor.IsAutoDiscovered;

    #endregion

    #region Metadata

    public IReadOnlyDictionary<string, object> Metadata { get; } = descriptor.Metadata ?? new Dictionary<string, object>();

    public T? GetMetadata<T>(string key)
    {
        return Metadata.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : default;
    }

    #endregion

    #region State Transitions

    public Task ActivateAsync()
    {
        if (_state != SubscriptionState.Pending && _state != SubscriptionState.Inactive)
        {
            throw new InvalidOperationException($"Cannot activate subscription in state {_state}");
        }

        _state = SubscriptionState.Active;
        _activatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        if (_state != SubscriptionState.Active)
        {
            throw new InvalidOperationException($"Cannot deactivate subscription in state {_state}");
        }

        _state = SubscriptionState.Inactive;
        _deactivatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task ReactivateAsync()
    {
        return ActivateAsync();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_state == SubscriptionState.Disposed)
            return;

        _state = SubscriptionState.Disposed;

        // Dispose handler factory if it's disposable
        if (HandlerFactory is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (HandlerFactory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
