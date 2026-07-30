using System.Reflection;
using System.Reflection.Emit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Models;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.Modules;

public sealed class ModuleConfigurationCompositionWorkTests
{
    [Fact]
    public void Composition_WhenAnalysisSucceeds_ShouldPublishDefinitionsAndBindOptions()
    {
        var definition = ConfigurationTypeDefinition.Create("PilotOptions", "Pilot", "test.pilot");
        var assembly = DynamicConfigurationAssembly.Create(definition);
        var optionsType = assembly.GetType(definition.TypeName, throwOnError: true)!;
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Pilot:Value"] = "ready";

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault().Add(assembly));
            monica.AddConfiguration();
        });

        using var host = builder.Build();
        var registry = host.Services.GetRequiredService<IConfigurationDefinitionRegistry>();
        var optionsServiceType = typeof(IOptions<>).MakeGenericType(optionsType);
        var optionsService = host.Services.GetRequiredService(optionsServiceType);
        var optionsValue = optionsServiceType.GetProperty(nameof(IOptions<object>.Value))!.GetValue(optionsService)!;

        registry.GetAll().Should().ContainSingle()
            .Which.DefinitionKey.Should().Be("test.pilot");
        optionsType.GetProperty("Value")!.GetValue(optionsValue).Should().Be("ready");
    }

    [Fact]
    public void Composition_ShouldPublishDefinitionsBeforeAnyPostServiceCallbackRuns()
    {
        var definition = ConfigurationTypeDefinition.Create("PilotOptions", "Pilot", "test.pilot");
        var assembly = DynamicConfigurationAssembly.Create(definition);
        var optionsType = assembly.GetType(definition.TypeName, throwOnError: true)!;
        var observedDefinitionCount = -1;
        var observedOptionsBinding = false;
        var observedOptionsValidator = false;
        var builder = Host.CreateApplicationBuilder();

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault().Add(assembly));
            monica.AddModule<
                DefinitionVisibilityProbeModule,
                DefinitionVisibilityProbeOption,
                DefinitionVisibilityProbeGuide>(options =>
                    options.Observe = services =>
                    {
                        observedDefinitionCount = GetRegisteredDefinitionRegistry(services).GetAll().Count;
                        observedOptionsBinding = services.Any(descriptor =>
                            descriptor.ServiceType == typeof(IConfigureOptions<>).MakeGenericType(optionsType));
                        observedOptionsValidator = services.Any(descriptor =>
                            descriptor.ServiceType == typeof(IValidateOptions<>).MakeGenericType(optionsType));
                    });
            monica.AddConfiguration(options =>
                options.RuntimeValidationBehavior = ConfigurationRuntimeValidationBehavior.FailFast);
        });

        observedDefinitionCount.Should().Be(1);
        observedOptionsBinding.Should().BeTrue();
        observedOptionsValidator.Should().BeTrue();
    }

    [Fact]
    public void Composition_WhenSectionPathsConflict_ShouldRejectWithoutPublishingPartialDefinitions()
    {
        var assembly = DynamicConfigurationAssembly.Create(
            ConfigurationTypeDefinition.Create("BetaOptions", "Shared", "test.beta"),
            ConfigurationTypeDefinition.Create("AlphaOptions", "Shared", "test.alpha"));
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault().Add(assembly));
            monica.AddConfiguration();
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*Configuration section path 'Shared' is used by both 'test.alpha' and 'test.beta'*");
        GetRegisteredDefinitionRegistry(builder.Services).GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Composition_WhenDuplicateSectionsAreExplicitlyAllowed_ShouldPublishAndBindEveryDefinition()
    {
        var assembly = DynamicConfigurationAssembly.Create(
            ConfigurationTypeDefinition.Create("BetaOptions", "Shared", "test.beta"),
            ConfigurationTypeDefinition.Create("AlphaOptions", "Shared", "test.alpha"));
        var builder = Host.CreateApplicationBuilder();

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault().Add(assembly));
            monica.AddConfiguration(options =>
                options.DuplicateSectionPathBehavior = ConfigurationDuplicateSectionPathBehavior.Warning);
        });

        using var host = builder.Build();
        var registry = host.Services.GetRequiredService<IConfigurationDefinitionRegistry>();

        registry.GetAll().Select(static definition => definition.DefinitionKey)
            .Should().Equal("test.alpha", "test.beta");
        foreach (var optionsType in assembly.GetTypes())
        {
            host.Services.GetService(typeof(IOptions<>).MakeGenericType(optionsType)).Should().NotBeNull();
        }
    }

    [Fact]
    public void Composition_WhenDefinitionKeysConflict_ShouldRejectDeterministicallyWithoutPublishing()
    {
        var assembly = DynamicConfigurationAssembly.Create(
            ConfigurationTypeDefinition.Create("BetaOptions", "Beta", "test.duplicate"),
            ConfigurationTypeDefinition.Create("AlphaOptions", "Alpha", "test.duplicate"));
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault().Add(assembly));
            monica.AddConfiguration();
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*Configuration definition key 'test.duplicate' is declared by multiple options types: AlphaOptions, BetaOptions*");
        GetRegisteredDefinitionRegistry(builder.Services).GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Composition_WhenOneDefinitionCannotBeScanned_ShouldPublishNothing()
    {
        var assembly = DynamicConfigurationAssembly.Create(
            ConfigurationTypeDefinition.Create("AlphaValidOptions", "Valid", "test.valid"),
            ConfigurationTypeDefinition.CreateInvalidList("OmegaInvalidOptions", "Invalid", "test.invalid"));
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault().Add(assembly));
            monica.AddConfiguration();
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*declares multiple list item key properties*");
        GetRegisteredDefinitionRegistry(builder.Services).GetAll().Should().BeEmpty();
        var validOptionsType = assembly.GetType("AlphaValidOptions", throwOnError: true)!;
        builder.Services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<>).MakeGenericType(validOptionsType));
    }

    private static IConfigurationDefinitionRegistry GetRegisteredDefinitionRegistry(IServiceCollection services)
    {
        return services.Last(descriptor => descriptor.ServiceType == typeof(IConfigurationDefinitionRegistry))
                   .ImplementationInstance as IConfigurationDefinitionRegistry
               ?? throw new InvalidOperationException("The module-owned definition registry was not registered as an instance.");
    }

    private sealed record ConfigurationTypeDefinition(
        string TypeName,
        string SectionPath,
        string DefinitionKey,
        bool HasInvalidList)
    {
        public static ConfigurationTypeDefinition Create(
            string typeName,
            string sectionPath,
            string definitionKey) => new(typeName, sectionPath, definitionKey, HasInvalidList: false);

        public static ConfigurationTypeDefinition CreateInvalidList(
            string typeName,
            string sectionPath,
            string definitionKey) => new(typeName, sectionPath, definitionKey, HasInvalidList: true);
    }

    [ModuleKey("Test.Monica.Configuration.DefinitionVisibilityProbe")]
    private sealed class DefinitionVisibilityProbeModule(DefinitionVisibilityProbeOption option)
        : ModuleBase<DefinitionVisibilityProbeModule, DefinitionVisibilityProbeOption, DefinitionVisibilityProbeGuide>(option)
    {
        public override void PostConfigureServices(IServiceCollection services)
        {
            Option.Observe?.Invoke(services);
        }
    }

    private sealed class DefinitionVisibilityProbeOption : ModuleOptions<DefinitionVisibilityProbeModule>
    {
        public Action<IServiceCollection>? Observe { get; set; }
    }

    private sealed class DefinitionVisibilityProbeGuide
        : ModuleGuide<DefinitionVisibilityProbeModule, DefinitionVisibilityProbeOption, DefinitionVisibilityProbeGuide>;

    private static class DynamicConfigurationAssembly
    {
        private static readonly ConstructorInfo CONFIGURATION_ATTRIBUTE_CONSTRUCTOR =
            typeof(ConfigurationAttribute).GetConstructor([typeof(string)])!;

        private static readonly PropertyInfo DEFINITION_KEY_PROPERTY =
            typeof(ConfigurationAttribute).GetProperty(nameof(ConfigurationAttribute.DefinitionKey))!;

        private static readonly ConstructorInfo OPTION_SETTING_ATTRIBUTE_CONSTRUCTOR =
            typeof(OptionSettingAttribute).GetConstructor(Type.EmptyTypes)!;

        private static readonly PropertyInfo IS_LIST_ITEM_KEY_PROPERTY =
            typeof(OptionSettingAttribute).GetProperty(nameof(OptionSettingAttribute.IsListItemKey))!;

        public static Assembly Create(params ConfigurationTypeDefinition[] definitions)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"Test.Monica.Configuration.Dynamic.{Guid.NewGuid():N}"),
                AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("Main");

            foreach (var definition in definitions)
            {
                DefineOptionsType(module, definition);
            }

            return assembly;
        }

        private static void DefineOptionsType(ModuleBuilder module, ConfigurationTypeDefinition definition)
        {
            var optionsType = module.DefineType(
                definition.TypeName,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
            optionsType.DefineDefaultConstructor(MethodAttributes.Public);
            optionsType.SetCustomAttribute(new CustomAttributeBuilder(
                CONFIGURATION_ATTRIBUTE_CONSTRUCTOR,
                [definition.SectionPath],
                [DEFINITION_KEY_PROPERTY],
                [definition.DefinitionKey]));

            var propertyType = definition.HasInvalidList
                ? typeof(List<>).MakeGenericType(DefineInvalidListItemType(module, $"{definition.TypeName}Item"))
                : typeof(string);
            DefineAutoProperty(optionsType, definition.HasInvalidList ? "Items" : "Value", propertyType);
            optionsType.CreateType();
        }

        private static Type DefineInvalidListItemType(ModuleBuilder module, string typeName)
        {
            var itemType = module.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
            itemType.DefineDefaultConstructor(MethodAttributes.Public);
            DefineAutoProperty(itemType, "Name", typeof(string), isListItemKey: true);
            DefineAutoProperty(itemType, "Code", typeof(string), isListItemKey: true);
            return itemType.CreateType()!;
        }

        private static void DefineAutoProperty(
            TypeBuilder type,
            string name,
            Type propertyType,
            bool isListItemKey = false)
        {
            var field = type.DefineField($"_{char.ToLowerInvariant(name[0])}{name[1..]}", propertyType, FieldAttributes.Private);
            var property = type.DefineProperty(name, PropertyAttributes.None, propertyType, null);
            var getter = type.DefineMethod(
                $"get_{name}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType,
                Type.EmptyTypes);
            var getterIl = getter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, field);
            getterIl.Emit(OpCodes.Ret);
            var setter = type.DefineMethod(
                $"set_{name}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(void),
                [propertyType]);
            var setterIl = setter.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, field);
            setterIl.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            property.SetSetMethod(setter);

            if (isListItemKey)
            {
                property.SetCustomAttribute(new CustomAttributeBuilder(
                    OPTION_SETTING_ATTRIBUTE_CONSTRUCTOR,
                    [],
                    [IS_LIST_ITEM_KEY_PROPERTY],
                    [true]));
            }
        }
    }
}
