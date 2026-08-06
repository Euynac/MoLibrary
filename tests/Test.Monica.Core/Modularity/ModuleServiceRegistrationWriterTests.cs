using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Services.Support;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleServiceRegistrationWriterTests
{
    [Fact]
    public void Writer_ServiceIdentityLookup_ShouldRemainHashIndexed()
    {
        var dictionaryFields = typeof(ModuleServiceRegistrationWriter)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(static field =>
                field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            .ToArray();

        dictionaryFields.Should().ContainSingle();
    }

    [Fact]
    public void TryAdd_WhenServiceTypesShareDifferentKeyIdentities_ShouldRespectNullKeyAsUnkeyed()
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Singleton<IWriterContract, InitialWriter>());
        services.Add(KeyedDescriptor("blue", typeof(BlueWriter), ServiceLifetime.Singleton));
        var writer = new ModuleServiceRegistrationWriter(services);

        writer.TryAdd(ServiceDescriptor.Transient<IWriterContract, ReplacementWriter>()).Should().BeFalse();
        writer.TryAdd(KeyedDescriptor("blue", typeof(ReplacementWriter), ServiceLifetime.Transient)).Should().BeFalse();
        writer.TryAdd(KeyedDescriptor("green", typeof(GreenWriter), ServiceLifetime.Singleton)).Should().BeTrue();
        writer.TryAdd(KeyedDescriptor(null, typeof(NullKeyWriter), ServiceLifetime.Singleton)).Should().BeFalse();
        writer.TryAdd(KeyedDescriptor(null, typeof(ReplacementWriter), ServiceLifetime.Transient)).Should().BeFalse();

        services.Should().HaveCount(3);
        writer.Contains(typeof(IWriterContract)).Should().BeTrue();
        writer.Contains(typeof(IWriterContract), "blue", isKeyedService: true).Should().BeTrue();
        writer.Contains(typeof(IWriterContract), "green", isKeyedService: true).Should().BeTrue();
        writer.Contains(typeof(IWriterContract), serviceKey: null, isKeyedService: true).Should().BeFalse();
    }

    [Fact]
    public void Replace_WhenIdentityExistsOrIsMissing_ShouldReplaceLastExactMatchOrAppend()
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Singleton<IWriterContract, InitialWriter>());
        services.Add(ServiceDescriptor.Transient<IWriterContract, BlueWriter>());
        services.Add(KeyedDescriptor("blue", typeof(BlueWriter), ServiceLifetime.Singleton));
        services.Add(ServiceDescriptor.Singleton<UnrelatedService, UnrelatedService>());
        var writer = new ModuleServiceRegistrationWriter(services);

        writer.Replace(ServiceDescriptor.Scoped<IWriterContract, ReplacementWriter>());
        writer.Replace(KeyedDescriptor("blue", typeof(ReplacementWriter), ServiceLifetime.Scoped));
        writer.Replace(KeyedDescriptor("green", typeof(GreenWriter), ServiceLifetime.Transient));

        services.Should().HaveCount(5);
        services[0].ImplementationType.Should().Be(typeof(InitialWriter));
        services[1].ImplementationType.Should().Be(typeof(ReplacementWriter));
        services[1].Lifetime.Should().Be(ServiceLifetime.Scoped);
        services[2].KeyedImplementationType.Should().Be(typeof(ReplacementWriter));
        services[2].Lifetime.Should().Be(ServiceLifetime.Scoped);
        services[4].ServiceKey.Should().Be("green");
        services[4].KeyedImplementationType.Should().Be(typeof(GreenWriter));
        writer.TryAdd(KeyedDescriptor("green", typeof(ReplacementWriter), ServiceLifetime.Singleton)).Should().BeFalse();
    }

    private static ServiceDescriptor KeyedDescriptor(
        object? key,
        Type implementationType,
        ServiceLifetime lifetime)
    {
        return ServiceDescriptor.DescribeKeyed(typeof(IWriterContract), key, implementationType, lifetime);
    }

    private interface IWriterContract;

    private sealed class InitialWriter : IWriterContract;

    private sealed class BlueWriter : IWriterContract;

    private sealed class GreenWriter : IWriterContract;

    private sealed class NullKeyWriter : IWriterContract;

    private sealed class ReplacementWriter : IWriterContract;

    private sealed class UnrelatedService;
}
