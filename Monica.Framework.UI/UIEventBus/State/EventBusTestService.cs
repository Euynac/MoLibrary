using System.Collections.Concurrent;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Services.Support;
using Monica.Core.Results;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.UIEventBus.Models;
using Monica.Framework.UI.UIEventBus.Support;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// Event bus test service for managing test publishing, listener sessions, and message collection.
/// </summary>
public sealed class EventBusTestService(
    IDistributedEventBus distributedEventBus,
    IEventSubscriptionRegistry subscriptionRegistry,
    EventBusProviderDiscoveryService providerDiscoveryService,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<ModuleEventBusUIOption> options,
    IStringLocalizer<EventBusResource> localizer,
    ILogger<EventBusTestService> logger) : IAsyncDisposable
{
    private const int DEFAULT_LEGACY_MESSAGE_COUNT = 100;

    private readonly ConcurrentDictionary<EventSubscriptionId, RuntimeListenerState> _runtimeListeners = new();
    private readonly ConcurrentQueue<ReceivedTestMessage> _receivedMessages = new();
    private readonly object _legacySubscriptionLock = new();
    private IEventSubscription? _activeSubscription;

    /// <summary>
    /// Have you subscribed through the standalone test panel.
    /// </summary>
    public bool IsSubscribed
    {
        get
        {
            lock (_legacySubscriptionLock)
            {
                return _activeSubscription != null;
            }
        }
    }

    /// <summary>
    /// The topic currently subscribed by the standalone test panel.
    /// </summary>
    public string? CurrentTopicName { get; private set; }

    /// <summary>
    /// Creates a formatted JSON example for the event type behind a subscription.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    /// <returns>A formatted JSON sample or an error result.</returns>
    public Res<string> CreateRuntimeJsonSample(EventSubscriptionId subscriptionId)
    {
        try
        {
            var subscription = GetSourceSubscription(subscriptionId);
            if (subscription is null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            var sample = CreateSampleValue(subscription.EventType, [], depth: 0);
            var json = JsonSerializer.Serialize(
                sample,
                jsonSerializerOptionsProvider.SerializerOptions.Clone(o => o.WriteIndented = true));

            return Res.Ok<string>(json ?? "{}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create EventBus test JSON sample for {SubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Test:CreateJsonSampleFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Validates that JSON can be deserialized into the selected subscription event type.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    /// <param name="json">JSON payload to validate.</param>
    /// <returns>A success result when deserialization succeeds.</returns>
    public Res ValidateRuntimePayload(EventSubscriptionId subscriptionId, string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Res.Fail(localizer["Services:Common:JsonContentRequired"]);
            }

            var subscription = GetSourceSubscription(subscriptionId);
            if (subscription is null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            _ = DeserializePayload(subscription.EventType, json);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EventBus test JSON validation failed for {SubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Test:JsonValidationFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Publishes a JSON payload through the exact bus scope and service key of the selected subscription.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    /// <param name="json">JSON payload to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result describing whether the publish succeeded.</returns>
    public async Task<Res> PublishRuntimePayloadAsync(
        EventSubscriptionId subscriptionId,
        string json,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Res.Fail(localizer["Services:Common:JsonContentRequired"]);
            }

            var subscription = GetSourceSubscription(subscriptionId);
            if (subscription is null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            var eventBusResult = GetEventBus(subscription);
            if (eventBusResult.IsFailed(out var providerError, out var eventBus))
            {
                return providerError;
            }

            var payload = DeserializePayload(subscription.EventType, json);
            await eventBus.PublishAsync(subscription.EventType, payload, subscription.TopicName, cancellationToken);

            logger.LogInformation(
                "Published EventBus UI runtime test payload for {EventType} on {TopicName} ({Scope}, ServiceKey: {ServiceKey})",
                subscription.EventType.GetCleanFullName(),
                subscription.TopicName,
                subscription.Scope,
                subscription.ServiceKey ?? "default");

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish EventBus UI runtime test payload for {SubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Test:PublishRuntimePayloadFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Starts a background listener for the selected subscription.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    /// <returns>A result containing the current listener snapshot.</returns>
    public async Task<Res<RuntimeTestListenerSnapshot>> StartRuntimeListenerAsync(EventSubscriptionId subscriptionId)
    {
        try
        {
            var subscription = GetSourceSubscription(subscriptionId);
            if (subscription is null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            if (_runtimeListeners.TryGetValue(subscriptionId, out var existingState))
            {
                return existingState.CreateSnapshot();
            }

            var state = new RuntimeListenerState(
                subscriptionId,
                subscription.EventType,
                subscription.TopicName,
                GetMessageLimit(),
                jsonSerializerOptionsProvider.SerializerOptions.Clone(o => o.WriteIndented = true));

            var descriptor = new EventSubscriptionDescriptor
            {
                ServiceKey = subscription.ServiceKey,
                EventType = subscription.EventType,
                TopicName = subscription.TopicName,
                Scope = subscription.Scope,
                IsAutoDiscovered = false,
                Metadata = EventBusTestMetadataKeys.CreateListenerMetadata(subscriptionId),
                HandlerFactory = new EventBusRuntimeTestHandlerFactory(
                    subscription.EventType,
                    subscription.Scope,
                    state.CaptureAsync)
            };

            var listenerSubscription = await subscriptionRegistry.SubscribeAsync(descriptor);
            state.AttachSubscription(listenerSubscription);

            if (!_runtimeListeners.TryAdd(subscriptionId, state))
            {
                await subscriptionRegistry.UnsubscribeAsync(listenerSubscription.Id);
                return _runtimeListeners[subscriptionId].CreateSnapshot();
            }

            logger.LogInformation(
                "Started EventBus UI runtime listener {ListenerSubscriptionId} for source subscription {SourceSubscriptionId}",
                listenerSubscription.Id,
                subscriptionId);

            return state.CreateSnapshot();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start EventBus UI runtime listener for {SubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Test:StartRuntimeListenerFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Stops the background listener for the selected subscription.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    /// <returns>A success result when the listener is stopped or did not exist.</returns>
    public async Task<Res> StopRuntimeListenerAsync(EventSubscriptionId subscriptionId)
    {
        try
        {
            if (!_runtimeListeners.TryGetValue(subscriptionId, out var state))
            {
                return Res.Ok();
            }

            if (state.ListenerSubscription is not null)
            {
                await subscriptionRegistry.UnsubscribeAsync(state.ListenerSubscription.Id);
            }

            _runtimeListeners.TryRemove(subscriptionId, out _);
            state.Clear();

            logger.LogInformation("Stopped EventBus UI runtime listener for source subscription {SubscriptionId}", subscriptionId);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop EventBus UI runtime listener for {SubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Test:StopRuntimeListenerFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Gets the current listener snapshot for the selected subscription.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    /// <returns>Current listener state.</returns>
    public RuntimeTestListenerSnapshot GetRuntimeListenerSnapshot(EventSubscriptionId subscriptionId)
    {
        return _runtimeListeners.TryGetValue(subscriptionId, out var state)
            ? state.CreateSnapshot()
            : new RuntimeTestListenerSnapshot();
    }

    /// <summary>
    /// Gets the ids of subscriptions that currently have active test listeners.
    /// </summary>
    /// <returns>Active source subscription ids.</returns>
    public IReadOnlySet<string> GetActiveRuntimeListenerIds()
    {
        return _runtimeListeners.Keys
            .Select(id => id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Clears retained listener messages for the selected subscription.
    /// </summary>
    /// <param name="subscriptionId">Selected subscription id.</param>
    public void ClearRuntimeListenerMessages(EventSubscriptionId subscriptionId)
    {
        if (_runtimeListeners.TryGetValue(subscriptionId, out var state))
        {
            state.Clear();
        }
    }

    /// <summary>
    /// Publish standalone test-panel message.
    /// </summary>
    public async Task<Res> PublishTestMessageAsync(string message, string? topicName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Res.Fail(localizer["Services:Common:MessageContentRequired"]);
            }

            if (string.IsNullOrWhiteSpace(topicName))
            {
                return Res.Fail(localizer["Services:Common:TopicNameRequired"]);
            }

            var testMessage = new TestEventMessage
            {
                Message = message,
                CreatedAt = DateTime.UtcNow,
                MessageId = Guid.NewGuid().ToString()
            };

            await distributedEventBus.PublishAsync(testMessage, topicName, cancellationToken);

            logger.LogInformation("已发布测试消息到主题 {TopicName}: {MessageId}", topicName, testMessage.MessageId);

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发布测试消息失败");
            return Res.Fail(localizer["Services:Test:PublishMessageFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Subscribe standalone test panel to a test topic.
    /// </summary>
    public async Task<Res<IEventSubscription>> SubscribeToTestTopicAsync(string topicName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return Res.Fail(localizer["Services:Common:TopicNameRequired"]);
            }

            lock (_legacySubscriptionLock)
            {
                if (_activeSubscription != null)
                {
                    return Res.Fail(localizer["Services:Test:ActiveSubscriptionExists"]);
                }
            }

            var subscription = await distributedEventBus.SubscribeAsync<TestEventMessage>((eventData, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var receivedMessage = new ReceivedTestMessage
                {
                    EventData = eventData,
                    ReceivedAt = DateTime.Now,
                    TopicName = topicName
                };

                _receivedMessages.Enqueue(receivedMessage);

                while (_receivedMessages.Count > DEFAULT_LEGACY_MESSAGE_COUNT)
                {
                    _receivedMessages.TryDequeue(out _);
                }

                logger.LogInformation("收到测试消息: {MessageId} from topic {TopicName}",
                    eventData.MessageId, topicName);

                return Task.CompletedTask;
            }, topicName);

            lock (_legacySubscriptionLock)
            {
                _activeSubscription = subscription;
                CurrentTopicName = topicName;
            }

            logger.LogInformation("已订阅测试主题: {TopicName}", topicName);
            return Res.Ok(_activeSubscription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "订阅测试主题失败");
            return Res.Fail(localizer["Services:Test:SubscribeFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Unsubscribe standalone test panel from its test topic.
    /// </summary>
    public async Task<Res> UnsubscribeFromTestTopicAsync()
    {
        try
        {
            IEventSubscription? subscription;

            lock (_legacySubscriptionLock)
            {
                subscription = _activeSubscription;
                _activeSubscription = null;
                CurrentTopicName = null;
            }

            if (subscription != null)
            {
                await subscriptionRegistry.UnsubscribeAsync(subscription.Id);
                logger.LogInformation("已取消订阅测试主题");
            }

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "取消订阅失败");
            return Res.Fail(localizer["Services:Test:UnsubscribeFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Get a list of received standalone test-panel messages.
    /// </summary>
    public List<ReceivedTestMessage> GetReceivedMessages()
    {
        return _receivedMessages.ToList();
    }

    /// <summary>
    /// Clear standalone test-panel received messages.
    /// </summary>
    public void ClearReceivedMessages()
    {
        _receivedMessages.Clear();
        logger.LogInformation("已清空接收到的测试消息");
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var sourceSubscriptionId in _runtimeListeners.Keys.ToList())
        {
            await StopRuntimeListenerAsync(sourceSubscriptionId);
        }

        await UnsubscribeFromTestTopicAsync();
        _receivedMessages.Clear();
        logger.LogInformation("EventBusTestService 已释放");
    }

    private IEventSubscription? GetSourceSubscription(EventSubscriptionId subscriptionId)
    {
        var subscription = subscriptionRegistry.GetById(subscriptionId);
        return subscription is not null && !EventBusTestMetadataKeys.IsTestListenerSubscription(subscription)
            ? subscription
            : null;
    }

    private Res<IEventBus> GetEventBus(IEventSubscription subscription)
    {
        if (subscription.Scope == EventSubscriptionScope.Local)
        {
            if (providerDiscoveryService.GetLocalProvider(subscription.ServiceKey).IsFailed(out var error, out var localProvider))
            {
                return error;
            }

            return Res.Ok<IEventBus>(localProvider);
        }

        if (providerDiscoveryService.GetDistributedProvider(subscription.ServiceKey).IsFailed(out var distributedError, out var distributedProvider))
        {
            return distributedError;
        }

        return Res.Ok<IEventBus>(distributedProvider);
    }

    private object DeserializePayload(Type eventType, string json)
    {
        return JsonSerializer.Deserialize(json, eventType, jsonSerializerOptionsProvider.SerializerOptions)
               ?? throw new InvalidOperationException(localizer["Services:Test:JsonDeserializeNull"]);
    }

    private object? CreateSampleValue(Type type, HashSet<Type> visitedTypes, int depth)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            return CreateSampleValue(underlyingType, visitedTypes, depth);
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(Guid))
        {
            return Guid.Empty;
        }

        if (type == typeof(DateTime))
        {
            return DateTime.UnixEpoch;
        }

        if (type == typeof(DateTimeOffset))
        {
            return DateTimeOffset.UnixEpoch;
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.Zero;
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(type);
        }

        if (type.IsNumeric())
        {
            return Activator.CreateInstance(type);
        }

        if (type == typeof(byte[]))
        {
            return Array.Empty<byte>();
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType is null ? [] : new[] { CreateSampleValue(elementType, visitedTypes, depth + 1) };
        }

        if (TryGetDictionaryValueType(type, out var dictionaryValueType))
        {
            return new Dictionary<string, object?>
            {
                ["key"] = CreateSampleValue(dictionaryValueType, visitedTypes, depth + 1)
            };
        }

        if (TryGetEnumerableElementType(type, out var collectionElementType))
        {
            return new[] { CreateSampleValue(collectionElementType, visitedTypes, depth + 1) };
        }

        if (type.IsAbstract || type.IsInterface || depth > 4 || !visitedTypes.Add(type))
        {
            return null;
        }

        var sample = new Dictionary<string, object?>();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0 || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            sample[GetJsonMemberName(property)] = CreateSampleValue(property.PropertyType, visitedTypes, depth + 1);
        }

        if (jsonSerializerOptionsProvider.SerializerOptions.IncludeFields)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                {
                    continue;
                }

                sample[GetJsonMemberName(field)] = CreateSampleValue(field.FieldType, visitedTypes, depth + 1);
            }
        }

        visitedTypes.Remove(type);
        return sample;
    }

    private string GetJsonMemberName(MemberInfo member)
    {
        var explicitName = member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        return jsonSerializerOptionsProvider.SerializerOptions.PropertyNamingPolicy?.ConvertName(member.Name) ?? member.Name;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            elementType = typeof(object);
            return false;
        }

        elementType = type.GetInterfaces()
            .Append(type)
            .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(candidate => candidate.GetGenericArguments()[0])
            .FirstOrDefault() ?? typeof(object);

        return elementType != typeof(object);
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionaryType = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate =>
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                && candidate.GetGenericArguments()[0] == typeof(string));

        valueType = dictionaryType?.GetGenericArguments()[1] ?? typeof(object);
        return dictionaryType is not null;
    }

    private int GetMessageLimit()
    {
        return Math.Max(1, options.Value.TestListenerMessageLimit);
    }

    private sealed class RuntimeListenerState(
        EventSubscriptionId sourceSubscriptionId,
        Type eventType,
        string topicName,
        int messageLimit,
        JsonSerializerOptions serializerOptions)
    {
        private readonly Queue<RuntimeTestEventMessage> _messages = new();
        private readonly object _messagesLock = new();

        public EventSubscriptionId SourceSubscriptionId { get; } = sourceSubscriptionId;
        public Type EventType { get; } = eventType;
        public string TopicName { get; } = topicName;
        public int MessageLimit { get; } = messageLimit;
        public JsonSerializerOptions SerializerOptions { get; } = serializerOptions;
        public DateTime StartedAt { get; } = DateTime.Now;
        public IEventSubscription? ListenerSubscription { get; private set; }

        public void AttachSubscription(IEventSubscription listenerSubscription)
        {
            ListenerSubscription = listenerSubscription;
        }

        public Task CaptureAsync(object eventData)
        {
            var message = new RuntimeTestEventMessage
            {
                ReceivedAt = DateTime.Now,
                TopicName = TopicName,
                EventType = EventType.GetCleanFullName(),
                PayloadJson = JsonSerializer.Serialize(
                                  eventData,
                                  EventType,
                                  SerializerOptions) ?? "{}"
            };

            lock (_messagesLock)
            {
                _messages.Enqueue(message);
                while (_messages.Count > MessageLimit)
                {
                    _messages.Dequeue();
                }
            }

            return Task.CompletedTask;
        }

        public RuntimeTestListenerSnapshot CreateSnapshot()
        {
            lock (_messagesLock)
            {
                return new RuntimeTestListenerSnapshot
                {
                    IsListening = true,
                    TopicName = TopicName,
                    StartedAt = StartedAt,
                    MessageCount = _messages.Count,
                    Messages = _messages.ToList()
                };
            }
        }

        public void Clear()
        {
            lock (_messagesLock)
            {
                _messages.Clear();
            }
        }
    }
}

/// <summary>
/// Test message received by the standalone test panel.
/// </summary>
public class ReceivedTestMessage
{
    /// <summary>
    /// Event data.
    /// </summary>
    public required TestEventMessage EventData { get; init; }

    /// <summary>
    /// Receive time in local time.
    /// </summary>
    public DateTime ReceivedAt { get; init; }

    /// <summary>
    /// Topic name.
    /// </summary>
    public required string TopicName { get; init; }
}
