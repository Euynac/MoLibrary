using Microsoft.Extensions.Localization;
using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Support;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Owns unified-version list selection and comparison state for the versions page.
/// </summary>
internal sealed class ConfigurationVersionsPageState(
    ConfigurationFacade facade,
    ISnackbar snackbar,
    IStringLocalizer<ConfigurationUIResource> localizer)
{
    private readonly List<ConfigurationUnifiedVersionSummary> _versions = [];
    private readonly Dictionary<VersionComparisonCacheKey, ConfigurationUnifiedVersionComparison> _comparisonCache = new();
    private long? _selectedOriginVersion;
    private long? _selectedTargetVersion;
    private long _comparisonRequestId;
    private long _loadRequestId;
    private ConfigurationVersionComparisonMode _comparisonMode = ConfigurationVersionComparisonMode.Current;

    public bool Loading { get; private set; } = true;

    public bool ComparisonLoading { get; private set; }

    public ConfigurationUnifiedVersionComparison? SelectedComparison { get; private set; }

    public long? SelectedHistoryVersion { get; private set; }

    public ConfigurationUnifiedVersionSummary? CurrentVersion => _versions.FirstOrDefault();

    public IReadOnlyList<ConfigurationUnifiedVersionSummary> HistoryVersions =>
        _versions.Count <= 1 ? [] : _versions.Skip(1).ToArray();

    public bool HasVersions => _versions.Count > 0;

    public bool HasSelectedHistoryVersion => SelectedHistoryVersion is { } version
                                             && _versions.Any(candidate => candidate.Version == version);

    public string CurrentVersionLabel => CurrentVersion is { } current
        ? ConfigurationUnifiedVersionJsonFormatter.VersionLabel(current.Version)
        : localizer["Common:States:Empty"];

    public long? PreviousComparisonVersion =>
        _comparisonMode == ConfigurationVersionComparisonMode.Previous ? SelectedHistoryVersion : null;

    public string ComparisonTitle =>
        _selectedOriginVersion is { } originVersion && _selectedTargetVersion is { } targetVersion
            ? _comparisonMode == ConfigurationVersionComparisonMode.Previous
                ? localizer["Versions:Compare:AgainstPrevious", VersionText(originVersion), VersionText(targetVersion)]
                : localizer["Versions:Compare:AgainstCurrent", VersionText(originVersion), VersionText(targetVersion)]
            : localizer["Versions:Compare:NoSelection"];

    public string ComparisonSubtitle => _comparisonMode == ConfigurationVersionComparisonMode.Previous
        ? localizer["Versions:Compare:PreviousPair"]
        : localizer["Versions:Compare:CurrentOnly"];

    public async Task LoadAsync()
    {
        var requestId = ++_loadRequestId;
        Loading = true;
        ComparisonLoading = false;
        SelectedComparison = null;
        _comparisonRequestId++;
        _comparisonCache.Clear();

        var result = await facade.GetUnifiedVersionsAsync(limit: 200);
        if (requestId != _loadRequestId)
        {
            return;
        }

        if (result.IsFailed(out var error, out var versions))
        {
            snackbar.Add($"{localizer["Common:Errors:LoadFailed"]}: {error.Message}", Severity.Error);
            _versions.Clear();
            ResetSelection();
        }
        else
        {
            _versions.Clear();
            _versions.AddRange(versions.OrderByDescending(static version => version.Version));
            SelectDefaultHistoryVersion();
        }

        Loading = false;
        if (SelectedHistoryVersion is not { } selectedVersion
            || _versions.FirstOrDefault(version => version.Version == selectedVersion) is not { } selectedSummary)
        {
            return;
        }

        if (_comparisonMode == ConfigurationVersionComparisonMode.Previous
            && PreviousVersion(selectedSummary) is { } previousVersion)
        {
            await LoadComparisonAsync(
                previousVersion.Version,
                selectedSummary.Version,
                selectedSummary.Version,
                ConfigurationVersionComparisonMode.Previous);
            return;
        }

        await LoadCurrentComparisonAsync(selectedVersion);
    }

    public Task SelectVersionAsync(long version)
    {
        return LoadCurrentComparisonAsync(version);
    }

    public Task CompareWithPreviousAsync(ConfigurationUnifiedVersionSummary version)
    {
        var previous = PreviousVersion(version);
        return previous is null
            ? Task.CompletedTask
            : LoadComparisonAsync(
                previous.Version,
                version.Version,
                version.Version,
                ConfigurationVersionComparisonMode.Previous);
    }

    private void SelectDefaultHistoryVersion()
    {
        var current = CurrentVersion;
        if (current is null)
        {
            SelectedHistoryVersion = null;
            return;
        }

        if (SelectedHistoryVersion is { } selected
            && selected != current.Version
            && _versions.Any(version => version.Version == selected))
        {
            return;
        }

        SelectedHistoryVersion = HistoryVersions.FirstOrDefault()?.Version;
        _comparisonMode = ConfigurationVersionComparisonMode.Current;
    }

    private async Task LoadCurrentComparisonAsync(long historyVersion)
    {
        var current = CurrentVersion;
        if (current is null || historyVersion == current.Version)
        {
            ResetSelection();
            return;
        }

        await LoadComparisonAsync(
            historyVersion,
            current.Version,
            historyVersion,
            ConfigurationVersionComparisonMode.Current);
    }

    private async Task LoadComparisonAsync(
        long originVersion,
        long targetVersion,
        long selectedHistoryVersion,
        ConfigurationVersionComparisonMode mode)
    {
        SelectedHistoryVersion = selectedHistoryVersion;
        _selectedOriginVersion = originVersion;
        _selectedTargetVersion = targetVersion;
        _comparisonMode = mode;

        var key = new VersionComparisonCacheKey(originVersion, targetVersion);
        if (_comparisonCache.TryGetValue(key, out var cached))
        {
            SelectedComparison = cached;
            ComparisonLoading = false;
            return;
        }

        var requestId = ++_comparisonRequestId;
        ComparisonLoading = true;
        SelectedComparison = null;

        try
        {
            var result = await facade.CompareUnifiedVersionsAsync(originVersion, targetVersion);
            if (requestId != _comparisonRequestId)
            {
                return;
            }

            if (result.IsFailed(out var error, out var comparison))
            {
                snackbar.Add($"{localizer["Common:Errors:LoadFailed"]}: {error.Message}", Severity.Error);
                return;
            }

            _comparisonCache[key] = comparison;
            SelectedComparison = comparison;
        }
        finally
        {
            if (requestId == _comparisonRequestId)
            {
                ComparisonLoading = false;
            }
        }
    }

    private void ResetSelection()
    {
        SelectedHistoryVersion = null;
        _selectedOriginVersion = null;
        _selectedTargetVersion = null;
        SelectedComparison = null;
    }

    private string VersionText(long version)
    {
        var summary = _versions.FirstOrDefault(candidate => candidate.Version == version);
        return summary is null
            ? ConfigurationUnifiedVersionJsonFormatter.VersionLabel(version)
            : ConfigurationUnifiedVersionDisplayFormatter.VersionText(summary);
    }

    private ConfigurationUnifiedVersionSummary? PreviousVersion(ConfigurationUnifiedVersionSummary version)
    {
        var index = _versions.FindIndex(candidate => candidate.Version == version.Version);
        return index >= 0 && index + 1 < _versions.Count ? _versions[index + 1] : null;
    }

    private enum ConfigurationVersionComparisonMode
    {
        Current,
        Previous
    }

    private readonly record struct VersionComparisonCacheKey(long OriginVersion, long TargetVersion);
}
