using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Builds source-aware validation reports for the current runtime configuration.
/// </summary>
internal sealed class ConfigurationRuntimeValidationService(
    IConfigurationDefinitionRegistry definitionRegistry,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    ConfigurationValueValidationEngine validationEngine,
    IConfigurationSourceInspector sourceInspector,
    MonicaConfigurationProviderAccessor providerAccessor,
    ConfigurationRuntimeContext runtimeContext)
    : IConfigurationRuntimeValidationService, IDisposable
{
    private static readonly ConfigurationValueValidationOptions RUNTIME_OPTIONS = new();
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<string, ConfigurationValidationReport> _definitionReports = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeReloadRevisionTracker _runtimeReloadRevisionTracker = new(runtimeContext);
    private ValidationCacheRevision _cachedRevision = ValidationCacheRevision.Empty;
    private ConfigurationValidationReport? _fullReport;

    /// <summary>
    /// Builds a validation report for every local configuration definition.
    /// </summary>
    /// <returns>The validation report.</returns>
    public ConfigurationValidationReport GetReport()
    {
        lock (_cacheLock)
        {
            while (true)
            {
                var revision = PrepareCache();
                if (_fullReport is not null)
                {
                    return _fullReport;
                }

                var definitionReports = definitionRegistry.GetAll()
                    .Select(GetOrBuildDefinitionReport)
                    .ToArray();
                if (GetCurrentRevision() != revision)
                {
                    ResetCache(GetCurrentRevision());
                    continue;
                }

                _fullReport = CreateReport(definitionReports.SelectMany(static report => report.Issues));
                return _fullReport;
            }
        }
    }

    /// <summary>
    /// Builds a validation report for one local configuration definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The validation report.</returns>
    public ConfigurationValidationReport GetReport(string definitionKey)
    {
        var definition = definitionRegistry.GetRequired(definitionKey);
        lock (_cacheLock)
        {
            while (true)
            {
                var revision = PrepareCache();
                var report = GetOrBuildDefinitionReport(definition);
                if (GetCurrentRevision() == revision)
                {
                    return report;
                }

                ResetCache(GetCurrentRevision());
            }
        }
    }

    private ValidationCacheRevision PrepareCache()
    {
        var revision = GetCurrentRevision();
        if (_cachedRevision != revision)
        {
            ResetCache(revision);
        }

        return revision;
    }

    private ValidationCacheRevision GetCurrentRevision()
    {
        return new ValidationCacheRevision(
            providerAccessor.SuccessfulProjectionRevision,
            _runtimeReloadRevisionTracker.Revision);
    }

    private void ResetCache(ValidationCacheRevision revision)
    {
        _cachedRevision = revision;
        _definitionReports.Clear();
        _fullReport = null;
    }

    private ConfigurationValidationReport GetOrBuildDefinitionReport(ConfigurationDefinition definition)
    {
        if (_definitionReports.TryGetValue(definition.DefinitionKey, out var report))
        {
            return report;
        }

        report = CreateReport(GetIssues(definition));
        _definitionReports[definition.DefinitionKey] = report;
        return report;
    }

    private static ConfigurationValidationReport CreateReport(IEnumerable<ConfigurationRuntimeValidationIssue> issues)
    {
        var immutableIssues = Array.AsReadOnly(issues.ToArray());
        return new ConfigurationValidationReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Issues = immutableIssues
        };
    }

    private IEnumerable<ConfigurationRuntimeValidationIssue> GetIssues(ConfigurationDefinition definition)
    {
        var json = seedFactory.CreateRuntimeJson(definition.Root, definition.SectionPath);
        using var document = JsonDocument.Parse(json);
        var issues = validationEngine.Validate(definition.Root, LogicalPath.Root, document.RootElement, RUNTIME_OPTIONS);
        foreach (var issue in issues)
        {
            yield return BuildRuntimeIssue(definition, issue);
        }
    }

    private ConfigurationRuntimeValidationIssue BuildRuntimeIssue(
        ConfigurationDefinition definition,
        ConfigurationValueValidationIssue issue)
    {
        var isSensitive = ConfigurationSchemaNavigator.IsSensitivePath(
            definition.Root,
            issue.LogicalPath);
        var sourceChain = sourceInspector.GetSourceChain(definition, issue.LogicalPath);
        var effectiveSource = sourceChain.Values.FirstOrDefault(value => value.IsEffective)?.Source;
        return new ConfigurationRuntimeValidationIssue
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            DefinitionCategory = definition.Category,
            LogicalPath = issue.LogicalPath,
            NodeDisplayName = GetNodeLabel(definition, issue.Node),
            ConfigurationPath = sourceChain.ConfigurationPath,
            Problem = issue.Message,
            EffectiveDisplayValue = isSensitive ? null : issue.DisplayValue,
            IsMissing = issue.IsMissing,
            IsSensitive = isSensitive,
            EffectiveSource = effectiveSource,
            SourceChain = sourceChain,
            ValidationRules = issue.ValidationRules
        };
    }

    private static string GetNodeLabel(ConfigurationDefinition definition, ConfigurationNodeDefinition node)
    {
        if (node.RelativePath.Depth == 0)
        {
            return definition.DisplayName;
        }

        return string.IsNullOrWhiteSpace(node.DisplayName) ? node.Name : node.DisplayName;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _runtimeReloadRevisionTracker.Dispose();
    }

    private readonly record struct ValidationCacheRevision(long ProjectionRevision, long RuntimeReloadRevision)
    {
        internal static ValidationCacheRevision Empty { get; } = new(-1, -1);
    }

    private sealed class RuntimeReloadRevisionTracker : IDisposable
    {
        private readonly IDisposable? _registration;
        private long _revision;

        internal RuntimeReloadRevisionTracker(ConfigurationRuntimeContext runtimeContext)
        {
            _registration = runtimeContext.Root is { } root
                ? ChangeToken.OnChange(root.GetReloadToken, () => Interlocked.Increment(ref _revision))
                : null;
        }

        internal long Revision => Interlocked.Read(ref _revision);

        public void Dispose()
        {
            _registration?.Dispose();
        }
    }
}
