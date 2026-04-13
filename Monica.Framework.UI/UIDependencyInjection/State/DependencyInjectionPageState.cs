using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Models;
using Monica.Framework.UI.UIDependencyInjection.Models;

namespace Monica.Framework.UI.UIDependencyInjection.State;

/// <summary>
/// Owns the mutable UI state for the dependency-injection diagnostics page.
/// </summary>
public sealed class DependencyInjectionPageState
{
    private static readonly HashSet<DependencyInjectionDescriptorScopeFilter> DefaultScopeFilters =
    [
        DependencyInjectionDescriptorScopeFilter.AutoRegistered,
        DependencyInjectionDescriptorScopeFilter.Other
    ];

    private static readonly HashSet<ServiceLifetime> DefaultLifetimeFilters =
    [
        ServiceLifetime.Singleton,
        ServiceLifetime.Scoped,
        ServiceLifetime.Transient
    ];

    private static readonly HashSet<DependencyInjectionDescriptorMarkerFilter> DefaultMarkerFilters =
    [
        DependencyInjectionDescriptorMarkerFilter.Standard,
        DependencyInjectionDescriptorMarkerFilter.Keyed,
        DependencyInjectionDescriptorMarkerFilter.Warnings,
        DependencyInjectionDescriptorMarkerFilter.Errors,
        DependencyInjectionDescriptorMarkerFilter.Rewritten
    ];

    /// <summary>
    /// Gets the latest diagnostics snapshot.
    /// </summary>
    public DependencyInjectionDiagnosticsSnapshot? Snapshot { get; private set; }

    /// <summary>
    /// Gets whether the page is currently loading a snapshot.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the latest load error when the last refresh failed.
    /// </summary>
    public string? LoadError { get; private set; }

    /// <summary>
    /// Gets the current fuzzy-search text.
    /// </summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the currently selected scope filters.
    /// </summary>
    public IReadOnlyCollection<DependencyInjectionDescriptorScopeFilter> ScopeFilters { get; private set; } =
        new HashSet<DependencyInjectionDescriptorScopeFilter>(DefaultScopeFilters);

    /// <summary>
    /// Gets the currently selected lifetime filters.
    /// </summary>
    public IReadOnlyCollection<ServiceLifetime> LifetimeFilters { get; private set; } =
        new HashSet<ServiceLifetime>(DefaultLifetimeFilters);

    /// <summary>
    /// Gets the currently selected marker filters.
    /// </summary>
    public IReadOnlyCollection<DependencyInjectionDescriptorMarkerFilter> MarkerFilters { get; private set; } =
        new HashSet<DependencyInjectionDescriptorMarkerFilter>(DefaultMarkerFilters);

    /// <summary>
    /// Gets whether a snapshot is currently available.
    /// </summary>
    public bool HasData => Snapshot != null;

    /// <summary>
    /// Gets the descriptors matching the current filters.
    /// </summary>
    public IReadOnlyList<DependencyInjectionDescriptorInfo> FilteredDescriptors =>
        Snapshot?.Descriptors.Where(MatchesFilters).ToArray() ?? [];

    /// <summary>
    /// Marks the page as loading and clears the last error.
    /// </summary>
    public void BeginLoad()
    {
        IsLoading = true;
        LoadError = null;
    }

    /// <summary>
    /// Stores a newly loaded snapshot.
    /// </summary>
    public void ApplySnapshot(DependencyInjectionDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Snapshot = snapshot;
        LoadError = null;
        IsLoading = false;
    }

    /// <summary>
    /// Stores a load failure while keeping the last successful snapshot.
    /// </summary>
    public void FailLoad(string error)
    {
        LoadError = error;
        IsLoading = false;
    }

    /// <summary>
    /// Updates the fuzzy-search text.
    /// </summary>
    public void SetSearchText(string? value)
    {
        SearchText = value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Updates the scope filter.
    /// </summary>
    public void SetScopeFilters(IReadOnlyCollection<DependencyInjectionDescriptorScopeFilter>? filters)
    {
        ScopeFilters = filters == null
            ? []
            : new HashSet<DependencyInjectionDescriptorScopeFilter>(filters);
    }

    /// <summary>
    /// Updates the lifetime filters.
    /// </summary>
    public void SetLifetimeFilters(IReadOnlyCollection<ServiceLifetime>? filters)
    {
        LifetimeFilters = filters == null
            ? []
            : new HashSet<ServiceLifetime>(filters);
    }

    /// <summary>
    /// Updates the marker filters.
    /// </summary>
    public void SetMarkerFilters(IReadOnlyCollection<DependencyInjectionDescriptorMarkerFilter>? filters)
    {
        MarkerFilters = filters == null
            ? []
            : new HashSet<DependencyInjectionDescriptorMarkerFilter>(filters);
    }

    private bool MatchesFilters(DependencyInjectionDescriptorInfo descriptor)
    {
        var descriptorScope = descriptor.IsAutoRegistered
            ? DependencyInjectionDescriptorScopeFilter.AutoRegistered
            : DependencyInjectionDescriptorScopeFilter.Other;

        if (!ScopeFilters.Contains(descriptorScope))
        {
            return false;
        }

        if (!LifetimeFilters.Contains(descriptor.Lifetime))
        {
            return false;
        }

        if (!MatchesMarkerFilters(descriptor))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return ContainsText(descriptor.ServiceType) ||
               ContainsText(descriptor.ServiceTypeDisplayName) ||
               ContainsText(descriptor.ServiceAssemblyName) ||
               ContainsText(descriptor.ServiceKey) ||
               ContainsText(descriptor.ImplementationType) ||
               ContainsText(descriptor.ImplementationTypeDisplayName) ||
               ContainsText(descriptor.ImplementationAssemblyName) ||
               ContainsText(descriptor.FactoryDisplay) ||
               ContainsText(descriptor.RewriteReason) ||
               descriptor.Rewrites.Any(MatchesRewrite) ||
               (descriptor.AutoRegistration != null && MatchesAutoRegistration(descriptor.AutoRegistration));
    }

    private bool MatchesMarkerFilters(DependencyInjectionDescriptorInfo descriptor)
    {
        if (MarkerFilters.Count == 0)
        {
            return false;
        }

        var hasMarker = false;

        if (descriptor.IsKeyedService)
        {
            hasMarker = true;
            if (MarkerFilters.Contains(DependencyInjectionDescriptorMarkerFilter.Keyed))
            {
                return true;
            }
        }

        if (descriptor.HasWarningIssues)
        {
            hasMarker = true;
            if (MarkerFilters.Contains(DependencyInjectionDescriptorMarkerFilter.Warnings))
            {
                return true;
            }
        }

        if (descriptor.HasErrorIssues)
        {
            hasMarker = true;
            if (MarkerFilters.Contains(DependencyInjectionDescriptorMarkerFilter.Errors))
            {
                return true;
            }
        }

        if (descriptor.WasRewritten)
        {
            hasMarker = true;
            if (MarkerFilters.Contains(DependencyInjectionDescriptorMarkerFilter.Rewritten))
            {
                return true;
            }
        }

        return !hasMarker && MarkerFilters.Contains(DependencyInjectionDescriptorMarkerFilter.Standard);
    }

    private bool MatchesAutoRegistration(DependencyInjectionAutoRegistrationInfo autoRegistration)
    {
        return ContainsText(autoRegistration.SourceImplementationType) ||
               ContainsText(autoRegistration.SourceImplementationTypeDisplayName) ||
               ContainsText(autoRegistration.SourceImplementationAssemblyName) ||
               ContainsText(autoRegistration.RegistrationMode.ToString()) ||
               ContainsText(autoRegistration.LifetimeSource.ToString()) ||
               autoRegistration.Issues.Any(MatchesIssue) ||
               autoRegistration.Rewrites.Any(MatchesRewrite) ||
               autoRegistration.ExposedServices.Any(service =>
                   ContainsText(service.ServiceType) ||
                   ContainsText(service.ServiceTypeDisplayName) ||
                   ContainsText(service.ServiceKey));
    }

    private bool MatchesIssue(DependencyInjectionAutoRegistrationIssueInfo issue)
    {
        return ContainsText(issue.Kind.ToString()) ||
               ContainsText(issue.SourceImplementationType) ||
               ContainsText(issue.SourceImplementationTypeDisplayName) ||
               ContainsText(issue.SourceImplementationAssemblyName) ||
               issue.ExposedServices.Any(service =>
                   ContainsText(service.ServiceType) ||
                   ContainsText(service.ServiceTypeDisplayName) ||
                   ContainsText(service.ServiceKey));
    }

    private bool MatchesRewrite(DependencyInjectionDescriptorRewriteInfo rewrite)
    {
        return ContainsText(rewrite.SourceModule) ||
               ContainsText(rewrite.Summary) ||
               ContainsText(rewrite.RewriteKind) ||
               ContainsText(rewrite.ProxyKind) ||
               ContainsText(rewrite.RegistrationStyle) ||
               ContainsText(rewrite.ImplementationType) ||
               ContainsText(rewrite.ImplementationTypeDisplayName) ||
               ContainsText(rewrite.ImplementationAssemblyName) ||
               rewrite.InterceptorTypes.Any(ContainsText) ||
               rewrite.InterceptorTypeDisplayNames.Any(ContainsText);
    }

    private bool ContainsText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
