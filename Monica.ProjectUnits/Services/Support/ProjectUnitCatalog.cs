using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Modules;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Models;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Services.Support;

/// <summary>
/// Owns project-unit discovery, dependency analysis, and request-filter state for one Monica host.
/// </summary>
internal sealed class ProjectUnitCatalog : IProjectUnitCatalog, IRequestFilter
{
    private readonly Dictionary<string, ProjectUnit> _unitsByFullName = [];
    private readonly Dictionary<string, ProjectUnit> _unitsByName = [];
    private readonly Dictionary<string, Type> _enumTypes = [];
    private readonly ConcurrentDictionary<string, byte> _disabledRequestUrls = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<Func<Type, ProjectUnit?>> _unitFactories;
    private ProjectUnitDocumentationResolver _documentation = new(null);

    internal ProjectUnitCatalog(ModuleProjectUnitsOption options)
    {
        Options = options;

        // Factory order is explicit and host-owned. More specific unit types precede their broader base categories.
        _unitFactories =
        [
            type => UnitCrudApplicationService.Create(type, this),
            type => UnitApplicationService.Create(type, this),
            type => UnitConfiguration.Create(type, this),
            type => UnitDomainEventHandler.Create(type, this),
            type => UnitLocalEventHandler.Create(type, this),
            type => UnitDomainEvent.Create(type, this),
            type => UnitRepository.Create(type, this),
            type => UnitEntity.Create(type, this),
            type => UnitDomainService.Create(type, this),
            type => UnitRecurringJob.Create(type, this),
            type => UnitTriggeredJob.Create(type, this),
            type => UnitRequestDto.Create(type, this)
        ];
    }

    internal ModuleProjectUnitsOption Options { get; }

    internal ILogger Logger => Options.Logger;

    internal ProjectUnitDocumentationResolver Documentation => _documentation;

    public IReadOnlyDictionary<string, Type> EnumTypes => _enumTypes;

    internal void SetDocumentationService(IXmlDocumentationService? documentationService)
    {
        _documentation = new ProjectUnitDocumentationResolver(documentationService);
    }

    /// <summary>
    /// Discovers project units while preserving the incoming business-type stream for downstream iterators.
    /// </summary>
    internal IEnumerable<Type> Discover(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            DiscoverProjectUnit(type);
            DiscoverEnum(type);
            yield return type;
        }
    }

    internal void ConnectUnits()
    {
        foreach (var unit in _unitsByFullName.Values)
        {
            unit.DoingConnect();
        }
    }

    internal ProjectUnit? ResolveConstructorDependency(Type parameterType, ProjectUnit dependentUnit)
    {
        if (FindByFullName(parameterType.FullName) is { } directDependency)
        {
            return directDependency;
        }

        if (!TryGetConfigurationUsage(parameterType, out var configurationType, out var usageType)
            || FindByFullName<UnitConfiguration>(configurationType.FullName) is not { } configurationUnit)
        {
            return null;
        }

        configurationUnit.RecordDependency(dependentUnit, usageType);
        return configurationUnit;
    }

    public IReadOnlyList<ProjectUnit> GetAllUnits()
    {
        return [.. _unitsByFullName.Values];
    }

    public IReadOnlyList<TProjectUnit> GetUnits<TProjectUnit>() where TProjectUnit : ProjectUnit
    {
        return [.. _unitsByFullName.Values.OfType<TProjectUnit>()];
    }

    public ProjectUnit? FindByFullName(string? typeFullName)
    {
        return !string.IsNullOrWhiteSpace(typeFullName)
               && _unitsByFullName.TryGetValue(typeFullName, out var unit)
            ? unit
            : null;
    }

    public ProjectUnit? FindByName(string? typeName)
    {
        return !string.IsNullOrWhiteSpace(typeName)
               && _unitsByName.TryGetValue(typeName, out var unit)
            ? unit
            : null;
    }

    public TProjectUnit? FindByFullName<TProjectUnit>(string? typeFullName) where TProjectUnit : ProjectUnit
    {
        return FindByFullName(typeFullName) as TProjectUnit;
    }

    public TProjectUnit? FindByName<TProjectUnit>(string? typeName) where TProjectUnit : ProjectUnit
    {
        return FindByName(typeName) as TProjectUnit;
    }

    public TAttribute? GetAttributeByFullName<TAttribute>(string? typeFullName)
        where TAttribute : Attribute, IUnitCachedAttribute
    {
        return FindByFullName(typeFullName)?.Attributes.OfType<TAttribute>().FirstOrDefault();
    }

    public TAttribute? GetAttributeByName<TAttribute>(string? typeName)
        where TAttribute : Attribute, IUnitCachedAttribute
    {
        return FindByName(typeName)?.Attributes.OfType<TAttribute>().FirstOrDefault();
    }

    public void Disable(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _disabledRequestUrls.TryAdd(url, 0);
    }

    public void Enable(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _disabledRequestUrls.TryRemove(url, out _);
    }

    public List<string> GetDisabledUrls()
    {
        return [.. _disabledRequestUrls.Keys];
    }

    internal bool IsRequestDisabled(string path)
    {
        return _disabledRequestUrls.ContainsKey(path);
    }

    private void DiscoverProjectUnit(Type type)
    {
        if (type is not { IsClass: true, FullName: not null, IsGenericType: false, IsAbstract: false })
        {
            return;
        }

        var unit = CreateUnit(type);
        if (unit is null)
        {
            return;
        }

        unit.PolishUnitInfo();
        _unitsByFullName.Add(unit.Key, unit);

        if (!_unitsByName.TryAdd(unit.Type.Name, unit))
        {
            Logger.LogError(
                "Duplicate project-unit name {UnitName}: {NewUnitType} conflicts with {ExistingUnitType}.",
                unit.Type.Name,
                unit.Key,
                _unitsByName[unit.Type.Name].Type.FullName);
        }
    }

    private void DiscoverEnum(Type type)
    {
        if (type.IsEnum && !type.IsGenericTypeDefinition && !type.ContainsGenericParameters)
        {
            _enumTypes.TryAdd(type.Name, type);
        }
    }

    private ProjectUnit? CreateUnit(Type type)
    {
        foreach (var factory in _unitFactories)
        {
            if (factory(type) is { } unit)
            {
                return unit;
            }
        }

        return null;
    }

    private static bool TryGetConfigurationUsage(
        Type parameterType,
        out Type configurationType,
        out EConfigurationUsageType usageType)
    {
        configurationType = null!;
        usageType = EConfigurationUsageType.Unknown;

        if (!parameterType.IsInterface
            || !parameterType.IsImplementInterfaceGeneric(typeof(IOptions<>), out var optionsInterface)
            || optionsInterface.GetGenericArguments().FirstOrDefault() is not { } discoveredConfigurationType)
        {
            return false;
        }

        configurationType = discoveredConfigurationType;
        usageType = parameterType.IsImplementInterfaceGeneric(typeof(IOptionsSnapshot<>))
            ? EConfigurationUsageType.OnlineSnapshot
            : parameterType.IsImplementInterfaceGeneric(typeof(IOptionsMonitor<>))
                ? EConfigurationUsageType.OnlineMonitor
                : EConfigurationUsageType.Offline;
        return true;
    }
}
