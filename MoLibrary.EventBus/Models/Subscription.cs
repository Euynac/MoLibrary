using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;

namespace MoLibrary.EventBus.Models;

/// <summary>
/// Concrete subscription implementation with lifecycle management.
/// </summary>
internal class Subscription : ISubscription
{
    private SubscriptionState _state;
    private DateTimeOffset? _activatedAt;
    private DateTimeOffset? _deactivatedAt;

    public Subscription(SubscriptionDescriptor descriptor)
    {
        Id = SubscriptionId.NewId();
        ServiceKey = descriptor.ServiceKey;
        EventType = descriptor.EventType;
        TopicName = descriptor.TopicName;
        HandlerType = descriptor.HandlerType;
        HandlerFactory = descriptor.HandlerFactory;
        Scope = descriptor.Scope;
        IsAutoDiscovered = descriptor.IsAutoDiscovered;
        Metadata = descriptor.Metadata ?? new Dictionary<string, object>();

        CreatedAt = DateTimeOffset.UtcNow;
        _state = SubscriptionState.Pending;
    }

    #region Identity

    public SubscriptionId Id { get; }
    public string? ServiceKey { get; }
    public Type EventType { get; }
    public string TopicName { get; }

    #endregion

    #region Handler Information

    public Type? HandlerType { get; }
    public IEventHandlerFactory HandlerFactory { get; }

    #endregion

    #region Scope Information

    public SubscriptionScope Scope { get; }

    #endregion

    #region Lifecycle

    public SubscriptionState State => _state;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ActivatedAt => _activatedAt;
    public DateTimeOffset? DeactivatedAt => _deactivatedAt;
    public bool IsAutoDiscovered { get; }

    #endregion

    #region Metadata

    public IReadOnlyDictionary<string, object> Metadata { get; }

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
