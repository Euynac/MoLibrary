using AwesomeAssertions;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.Core.ObjectMapping.Facades;
using Monica.Core.ObjectMapping.Providers.Mapster;
using Monica.Core.Results;
using Monica.Core.TypeDiscovery.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.ObjectMapping;

public sealed class ModuleObjectMappingTests
{
    [Fact]
    public void Build_ShouldRegisterTheHostOwnedProfileCatalogAsSingleton()
    {
        using var host = BuildHost();

        var catalog = host.Services.GetRequiredService<MapsterProfileCatalog>();

        host.Services.GetRequiredService<MapsterProfileCatalog>().Should().BeSameAs(catalog);
        catalog.GetOrderedProfileTypes([], CreateTypeDependencyOrderer())
            .Should().Contain(typeof(InternalMappingProfile));
    }

    [Fact]
    public void Build_GenericHostWithAutomaticallyDiscoveredInternalProfile_ShouldProvideObjectMapping()
    {
        using var host = BuildHost();
        using var scope = host.Services.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IObjectMapper>();

        mapper.Map<MappingDestination>(new MappingSource { Value = "flight" })
            .Value.Should().Be("flight-profile");
    }

    [Fact]
    public void Build_WhenExplicitProfileOwnsPair_ShouldApplyAutomaticRefinementAfterIt()
    {
        using var host = BuildHost(mapping => mapping.AddProfile<ExplicitOwnerProfile>());
        using var scope = host.Services.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IObjectMapper>();

        var destination = mapper.Map<ExplicitFirstDestination>(new ExplicitFirstSource());

        destination.ExplicitLayer.Should().Be("explicit");
        destination.AutomaticLayer.Should().Be("automatic");
    }

    [Fact]
    public void Catalog_WhenExplicitProfileIsAlsoDiscovered_ShouldKeepItOnceBeforeAutomaticProfiles()
    {
        var catalog = new MapsterProfileCatalog();
        catalog.Discover(typeof(InternalMappingProfile));
        catalog.Discover(typeof(AutomaticRefinementProfile));

        var profiles = catalog.GetOrderedProfileTypes(
            [typeof(InternalMappingProfile)],
            CreateTypeDependencyOrderer());

        profiles.Should().Equal(typeof(InternalMappingProfile), typeof(AutomaticRefinementProfile));
    }

    [Fact]
    public void Catalog_WhenProfilesCannotBeActivated_ShouldIgnoreAbstractAndOpenGenericTypes()
    {
        var catalog = new MapsterProfileCatalog();

        catalog.Discover(typeof(AbstractMappingProfile));
        catalog.Discover(typeof(OpenGenericMappingProfile<>));

        catalog.GetOrderedProfileTypes([], CreateTypeDependencyOrderer())
            .Should().BeEmpty();
    }

    [Fact]
    public void Build_WhenProfileIsRegisteredTwice_ShouldApplyItOnce()
    {
        CountingMappingProfile.Reset();

        using var host = BuildHost(mapping => mapping
            .AddProfile<CountingMappingProfile>()
            .AddProfile<CountingMappingProfile>());
        using var scope = host.Services.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IObjectMapper>();

        mapper.Map<CountingDestination>(new CountingSource())
            .RegistrationCount.Should().Be(1);
        CountingMappingProfile.RegistrationCount.Should().Be(1);
    }

    [Fact]
    public void Build_WhenProfilesRefineOnePair_ShouldUseRegistrationOrder()
    {
        RefinementRegistrationTrace.Reset();

        using var host = BuildHost(mapping => mapping
            .AddProfile<PlatformRefinementProfile>()
            .AddProfile<DomainRefinementProfile>()
            .AddProfile<AdapterRefinementProfile>());
        using var scope = host.Services.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IObjectMapper>();

        var destination = mapper.Map<RefinementDestination>(new RefinementSource());

        destination.PlatformLayer.Should().Be("platform");
        destination.DomainLayer.Should().Be("domain");
        destination.AdapterLayer.Should().Be("adapter");
        RefinementRegistrationTrace.Snapshot().Should().Equal("platform", "domain", "adapter");
    }

    [Fact]
    public void AddMonica_WhenMultipleMappingsAreInvalid_ShouldReportEveryCompilationError()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
            monica.AddObjectMapping()
                .AddProfile<InvalidMappingProfile>());

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage($"*{nameof(InvalidDestinationOne)}*")
            .WithMessage($"*{nameof(InvalidDestinationTwo)}*");
    }

    [Fact]
    public void AddMonica_WhenProfileConstructorThrows_ShouldReportModuleCompositionFailure()
    {
        Action compose = () => BuildHost(mapping => mapping.AddProfile<ThrowingConstructorProfile>());

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*Module registration errors:*")
            .WithMessage("*constructor-profile-failure*");
    }

    [Fact]
    public void AddMonica_WhenProfileRegisterThrows_ShouldReportModuleCompositionFailure()
    {
        Action compose = () => BuildHost(mapping => mapping.AddProfile<ThrowingRegisterProfile>());

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*Module registration errors:*")
            .WithMessage("*register-profile-failure*");
    }

    [Fact]
    public async Task Build_TwoHostsWithConflictingProfiles_ShouldRemainIsolatedAndLeaveGlobalSettingsUntouched()
    {
        GlobalSettingsContains<HostMappingSource, HostMappingDestination>().Should().BeFalse();

        var firstHostTask = Task.Run(
            () => BuildHost(mapping => mapping.AddProfile<FirstHostMappingProfile>()),
            TestContext.Current.CancellationToken);
        var secondHostTask = Task.Run(
            () => BuildHost(mapping => mapping.AddProfile<SecondHostMappingProfile>()),
            TestContext.Current.CancellationToken);
        var hosts = await Task.WhenAll(firstHostTask, secondHostTask);

        try
        {
            using var firstScope = hosts[0].Services.CreateScope();
            using var secondScope = hosts[1].Services.CreateScope();
            var firstMapper = firstScope.ServiceProvider.GetRequiredService<IObjectMapper>();
            var secondMapper = secondScope.ServiceProvider.GetRequiredService<IObjectMapper>();

            firstMapper.Map<HostMappingDestination>(new HostMappingSource()).Value.Should().Be("first-host");
            secondMapper.Map<HostMappingDestination>(new HostMappingSource()).Value.Should().Be("second-host");
            hosts[0].Services.GetRequiredService<TypeAdapterConfig>()
                .Should().NotBeSameAs(hosts[1].Services.GetRequiredService<TypeAdapterConfig>());
            GlobalSettingsContains<HostMappingSource, HostMappingDestination>().Should().BeFalse();
        }
        finally
        {
            foreach (var host in hosts)
            {
                host.Dispose();
            }
        }
    }

    [Fact]
    public async Task Diagnostics_WhenMappingsAreInspected_ShouldNotMutateLiveConfiguration()
    {
        using var host = BuildHost();
        using var scope = host.Services.CreateScope();
        var config = host.Services.GetRequiredService<TypeAdapterConfig>();
        var serviceMapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        var facade = scope.ServiceProvider.GetRequiredService<ObjectMappingFacade>();

        config.SelfContainedCodeGeneration.Should().BeFalse();
        serviceMapper.Config.Should().BeSameAs(config);

        var result = await facade.GetStatusAsync();

        result.Status.Should().Be(ResStatus.Ok);
        result.Data.Should().NotBeNull();
        result.Data!.Mappings.Should().ContainSingle(mapping =>
            mapping.SourceType.Contains(nameof(MappingSource), StringComparison.Ordinal) &&
            mapping.DestinationType.Contains(nameof(MappingDestination), StringComparison.Ordinal));
        config.SelfContainedCodeGeneration.Should().BeFalse();
    }

    private static IHost BuildHost(Action<ModuleObjectMappingGuide>? configureMapping = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(ModuleObjectMappingTests).Assembly));
            var mapping = monica.AddObjectMapping();
            configureMapping?.Invoke(mapping);
        });
        return builder.Build();
    }

    private static bool GlobalSettingsContains<TSource, TDestination>()
    {
        return TypeAdapterConfig.GlobalSettings.RuleMap.Keys.Any(key =>
            key.Source == typeof(TSource) && key.Destination == typeof(TDestination));
    }

    private static TypeDependencyOrderer CreateTypeDependencyOrderer()
    {
        return new TypeDependencyOrderer([typeof(ModuleObjectMappingTests).Assembly]);
    }

    private sealed class InternalMappingProfile : IRegister
    {
        private InternalMappingProfile()
        {
        }

        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<MappingSource, MappingDestination>()
                .Map(destination => destination.Value, source => $"{source.Value}-profile");
        }
    }

    private sealed class AutomaticRefinementProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.ForType<ExplicitFirstSource, ExplicitFirstDestination>()
                .Map(destination => destination.AutomaticLayer, _ => "automatic");
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class ExplicitOwnerProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<ExplicitFirstSource, ExplicitFirstDestination>()
                .Map(destination => destination.ExplicitLayer, _ => "explicit");
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class CountingMappingProfile : IRegister
    {
        private static int _registrationCount;

        public static int RegistrationCount => Volatile.Read(ref _registrationCount);

        public static void Reset()
        {
            Interlocked.Exchange(ref _registrationCount, 0);
        }

        public void Register(TypeAdapterConfig config)
        {
            var registrationCount = Interlocked.Increment(ref _registrationCount);
            config.NewConfig<CountingSource, CountingDestination>()
                .Map(destination => destination.RegistrationCount, _ => registrationCount);
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class PlatformRefinementProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            RefinementRegistrationTrace.Record("platform");
            config.NewConfig<RefinementSource, RefinementDestination>()
                .Map(destination => destination.PlatformLayer, _ => "platform");
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class DomainRefinementProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            RefinementRegistrationTrace.Record("domain");
            config.ForType<RefinementSource, RefinementDestination>()
                .Map(destination => destination.DomainLayer, _ => "domain");
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class AdapterRefinementProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            RefinementRegistrationTrace.Record("adapter");
            config.ForType<RefinementSource, RefinementDestination>()
                .Map(destination => destination.AdapterLayer, _ => "adapter");
        }
    }

    private static class RefinementRegistrationTrace
    {
        private static readonly Lock _sync = new();
        private static readonly List<string> _registrations = [];

        public static void Reset()
        {
            lock (_sync)
            {
                _registrations.Clear();
            }
        }

        public static void Record(string profile)
        {
            lock (_sync)
            {
                _registrations.Add(profile);
            }
        }

        public static IReadOnlyList<string> Snapshot()
        {
            lock (_sync)
            {
                return [.. _registrations];
            }
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class InvalidMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.RequireExplicitMapping = true;
            config.NewConfig<InvalidSourceOne, InvalidDestinationOne>();
            config.NewConfig<InvalidSourceTwo, InvalidDestinationTwo>();
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class ThrowingConstructorProfile : IRegister
    {
        private ThrowingConstructorProfile()
        {
            throw new InvalidOperationException("constructor-profile-failure");
        }

        public void Register(TypeAdapterConfig config)
        {
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class ThrowingRegisterProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            throw new InvalidOperationException("register-profile-failure");
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class FirstHostMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<HostMappingSource, HostMappingDestination>()
                .Map(destination => destination.Value, _ => "first-host");
        }
    }

    [ExcludeFromBusinessTypeDiscovery]
    private sealed class SecondHostMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<HostMappingSource, HostMappingDestination>()
                .Map(destination => destination.Value, _ => "second-host");
        }
    }

    private sealed class MappingSource
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class MappingDestination
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ExplicitFirstSource;

    private sealed class ExplicitFirstDestination
    {
        public string ExplicitLayer { get; set; } = string.Empty;

        public string AutomaticLayer { get; set; } = string.Empty;
    }

    private sealed class CountingSource;

    private sealed class CountingDestination
    {
        public int RegistrationCount { get; set; }
    }

    private sealed class RefinementSource;

    private sealed class RefinementDestination
    {
        public string PlatformLayer { get; set; } = string.Empty;
        public string DomainLayer { get; set; } = string.Empty;
        public string AdapterLayer { get; set; } = string.Empty;
    }

    private sealed class InvalidSourceOne
    {
        public InvalidNestedSourceOne Nested { get; set; } = new();
    }

    private sealed class InvalidDestinationOne
    {
        public InvalidNestedDestinationOne Nested { get; set; } = new();
    }

    private sealed class InvalidNestedSourceOne;

    private sealed class InvalidNestedDestinationOne;

    private sealed class InvalidSourceTwo
    {
        public InvalidNestedSourceTwo Nested { get; set; } = new();
    }

    private sealed class InvalidDestinationTwo
    {
        public InvalidNestedDestinationTwo Nested { get; set; } = new();
    }

    private sealed class InvalidNestedSourceTwo;

    private sealed class InvalidNestedDestinationTwo;

    private sealed class HostMappingSource;

    private sealed class HostMappingDestination
    {
        public string Value { get; set; } = string.Empty;
    }

    private abstract class AbstractMappingProfile : IRegister
    {
        public abstract void Register(TypeAdapterConfig config);
    }

    private sealed class OpenGenericMappingProfile<T> : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
        }
    }

}
