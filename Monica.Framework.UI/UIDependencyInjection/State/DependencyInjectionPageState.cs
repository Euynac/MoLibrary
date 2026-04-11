using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Models;
using Monica.Framework.UI.UIDependencyInjection.Models;

namespace Monica.Framework.UI.UIDependencyInjection.State;

/// <summary>
/// Owns the mutable UI state for the dependency-injection diagnostics page.
/// </summary>
public sealed class DependencyInjectionPageState
{
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
    /// Gets the current scope filter.
    /// </summary>
    public DependencyInjectionDescriptorScopeFilter ScopeFilter { get; private set; }

    /// <summary>
    /// Gets whether only keyed descriptors should be shown.
    /// </summary>
    public bool KeyedOnly { get; private set; }

    /// <summary>
    /// Gets the lifetime filter when one is selected.
    /// </summary>
    public ServiceLifetime? LifetimeFilter { get; private set; }

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
    public void SetScopeFilter(DependencyInjectionDescriptorScopeFilter filter)
    {
        ScopeFilter = filter;
    }

    /// <summary>
    /// Updates the keyed-only filter.
    /// </summary>
    public void SetKeyedOnly(bool value)
    {
        KeyedOnly = value;
    }

    /// <summary>
    /// Updates the lifetime filter.
    /// </summary>
    public void SetLifetimeFilter(ServiceLifetime? value)
    {
        LifetimeFilter = value;
    }

    private bool MatchesFilters(DependencyInjectionDescriptorInfo descriptor)
    {
        if (ScopeFilter == DependencyInjectionDescriptorScopeFilter.AutoRegistered && !descriptor.IsAutoRegistered)
        {
            return false;
        }

        if (ScopeFilter == DependencyInjectionDescriptorScopeFilter.Other && descriptor.IsAutoRegistered)
        {
            return false;
        }

        if (KeyedOnly && !descriptor.IsKeyedService)
        {
            return false;
        }

        if (LifetimeFilter != null && descriptor.Lifetime != LifetimeFilter.Value)
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
               (descriptor.AutoRegistration != null && MatchesAutoRegistration(descriptor.AutoRegistration));
    }

    private bool MatchesAutoRegistration(DependencyInjectionAutoRegistrationInfo autoRegistration)
    {
        return ContainsText(autoRegistration.SourceImplementationType) ||
               ContainsText(autoRegistration.SourceImplementationTypeDisplayName) ||
               ContainsText(autoRegistration.SourceImplementationAssemblyName) ||
               autoRegistration.ExposedServices.Any(service =>
                   ContainsText(service.ServiceType) ||
                   ContainsText(service.ServiceTypeDisplayName) ||
                   ContainsText(service.ServiceKey));
    }

    private bool ContainsText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
