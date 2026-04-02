using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Monica.EventBus.Models.Internal;

namespace Monica.EventBus.Services.Support;

internal sealed class EventBusAutoDiscovery
{
    private readonly List<EventHandlerRegistration> _registrations = [];

    public void Collect(Type type)
    {
        if (type is not { IsClass: true, IsAbstract: false } || !typeof(IEventHandler).IsAssignableFrom(type))
        {
            return;
        }

        _registrations.AddRange(EventHandlerRegistration.CreateFromHandlerType(type));
    }

    public IReadOnlyList<EventSubscriptionDescriptor> BuildDescriptors(IServiceScopeFactory serviceScopeFactory)
    {
        return _registrations
            .Where(registration => registration.IsAutoRegistered)
            .Select(registration => new EventSubscriptionDescriptor
            {
                ServiceKey = null,
                EventType = registration.EventType,
                TopicName = registration.TopicName,
                HandlerFactory = new IocEventHandlerFactory(serviceScopeFactory, registration.HandlerType),
                Scope = registration.IsDistributed ? EventSubscriptionScope.Distributed : EventSubscriptionScope.Local,
                IsAutoDiscovered = true
            })
            .ToList();
    }
}
