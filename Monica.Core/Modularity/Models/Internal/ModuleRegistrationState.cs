using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Stores the mutable declaration draft for one module until composition is compiled.
/// </summary>
internal sealed class ModuleRegistrationState
{
    private readonly List<OptionContribution> _optionContributions = [];
    private readonly Dictionary<string, List<Action<object>>> _profileContributions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _profiles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requiredFeatures = new(StringComparer.Ordinal);
    private readonly HashSet<string> _satisfiedFeatures = new(StringComparer.Ordinal);
    private readonly HashSet<string> _keyedServiceKeys = new(StringComparer.Ordinal);
    private readonly List<string> _webHostRequirementReasons = [];
    private readonly List<ModuleConfigurationRequest> _configurationRequests = [];
    private IModuleOptions? _moduleOption;
    private long _requestOrdinal;
    private Func<object>? _optionFactory;

    internal ModuleRegistrationState(MonicaApplication application, Type moduleType)
    {
        Application = application;
        ModuleType = moduleType;
    }

    internal MonicaApplication Application { get; }

    /// <summary>
    /// Gets the concrete module type.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    /// Gets the latest composition phase reached by the module.
    /// </summary>
    public ModulePhase ModulePhase { get; private set; }

    /// <summary>
    /// Gets the stable dependency-first execution order.
    /// </summary>
    public int Order { get; internal set; } = 1000;

    /// <summary>
    /// Gets the ordered lifecycle contributions owned by this module.
    /// </summary>
    internal IReadOnlyList<ModuleConfigurationRequest> ConfigurationRequests => _configurationRequests;

    /// <summary>
    /// Gets the primary module option type.
    /// </summary>
    public Type ModuleOptionType { get; private set; } = null!;

    /// <summary>
    /// Gets the finalized primary module option.
    /// </summary>
    public IModuleOptions ModuleOption => _moduleOption
        ?? throw new InvalidOperationException($"Module {ModuleType.Name} options have not been finalized.");

    /// <summary>
    /// Gets keyed service identities published by this module.
    /// </summary>
    internal IReadOnlySet<string> KeyedServiceKeys => _keyedServiceKeys;

    /// <summary>
    /// Gets the single host-owned module strategy instance.
    /// </summary>
    public MonicaModule ModuleSingleton { get; private set; } = null!;

    internal string? DisabledReason { get; private set; }

    internal bool RequiresWebHost =>
        ModuleSingleton.RequiresWebHost || _webHostRequirementReasons.Count != 0;

    internal string? WebHostRequirementReason
    {
        get
        {
            var declaredReasons = string.Join("; ", _webHostRequirementReasons);
            if (!ModuleSingleton.RequiresWebHost)
            {
                return declaredReasons.Length == 0 ? null : declaredReasons;
            }

            const string intrinsicReason =
                "The module declares an intrinsic ASP.NET Core web lifecycle requirement.";
            return declaredReasons.Length == 0
                ? intrinsicReason
                : $"{intrinsicReason} {declaredReasons}";
        }
    }

    internal bool IsFinalized => _moduleOption is not null;

    internal void Initialize<TModule, TOptions>()
        where TModule : MonicaModule<TOptions>, new()
        where TOptions : ModuleOptions<TModule>, new()
    {
        if (_optionFactory is not null)
        {
            if (ModuleOptionType != typeof(TOptions) || ModuleType != typeof(TModule))
            {
                throw new InvalidOperationException(
                    $"Module {ModuleType.Name} was registered with conflicting option metadata.");
            }

            return;
        }

        ModuleOptionType = typeof(TOptions);
        ModuleSingleton = new TModule();
        _optionFactory = static () => new TOptions();
    }

    internal void AddOptionContribution<TOptions>(Type? configuredBy, Action<TOptions> configure)
        where TOptions : class, IModuleOptions, new()
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureNotFinalized();
        _optionContributions.Add(new OptionContribution(
            IsHostContribution: configuredBy is null,
            Ordinal: _optionContributions.Count,
            Apply: option => configure((TOptions)option)));
    }

    internal void AddProfileContribution<TOptions>(string name, Action<TOptions> configure)
        where TOptions : class, IModuleOptions, new()
    {
        EnsureNotFinalized();
        if (!_profileContributions.TryGetValue(name, out var contributions))
        {
            contributions = [];
            _profileContributions.Add(name, contributions);
        }

        contributions.Add(option => configure((TOptions)option));
    }

    internal TOptions GetProfile<TOptions>(string name)
        where TOptions : class, IModuleOptions, new()
    {
        if (!IsFinalized)
        {
            throw new InvalidOperationException(
                $"Named option profile '{name}' for {ModuleType.Name} has not been finalized.");
        }

        return _profiles.TryGetValue(name, out var profile)
            ? (TOptions)profile
            : throw new KeyNotFoundException(
                $"Named option profile '{name}' was not declared for {ModuleType.Name}.");
    }

    internal object GetOptionOrDefault(string? profileName)
    {
        if (!IsFinalized)
        {
            throw new InvalidOperationException(
                $"Module {ModuleType.Name} options have not been finalized.");
        }

        return profileName is not null && _profiles.TryGetValue(profileName, out var profile)
            ? profile
            : ModuleOption;
    }

    internal void RequireFeature(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        EnsureNotFinalized();
        _requiredFeatures.Add(featureName);
    }

    internal void SatisfyFeature(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        EnsureNotFinalized();
        _satisfiedFeatures.Add(featureName);
    }

    internal void AddKeyedServiceKey(string serviceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
        EnsureNotFinalized();
        _keyedServiceKeys.Add(serviceKey);
    }

    internal void MarkDisabled(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureNotFinalized();
        DisabledReason ??= reason;
        ModulePhase = ModulePhase.Disabled;
    }

    internal void RequireWebHost(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureNotFinalized();
        if (!_webHostRequirementReasons.Contains(reason, StringComparer.Ordinal))
        {
            _webHostRequirementReasons.Add(reason);
        }
    }

    internal void FinalizeOptions()
    {
        if (_optionFactory is null)
        {
            throw new InvalidOperationException($"Module {ModuleType.Name} has no option factory.");
        }

        if (IsFinalized)
        {
            throw new InvalidOperationException($"Module {ModuleType.Name} was already finalized.");
        }

        var option = CreateBoundOption();
        foreach (var contribution in _optionContributions
                     .OrderBy(static contribution => contribution.IsHostContribution)
                     .ThenBy(static contribution => contribution.Ordinal))
        {
            contribution.Apply(option);
        }

        _moduleOption = (IModuleOptions)option;
        foreach (var (name, contributions) in _profileContributions)
        {
            var profile = CreateBoundOption();
            foreach (var contribution in contributions)
            {
                contribution(profile);
            }

            _profiles.Add(name, profile);
        }

        ModuleSingleton.BindOptions(Application, option);
        ModuleSingleton.ValidateFinalOptions(option, profileName: null);
        foreach (var (name, profile) in _profiles)
        {
            ModuleSingleton.ValidateFinalOptions(profile, name);
        }

        ModuleSingleton.AttachLifecycle(this);
    }

    internal void AddLifecycleRequest(
        ModulePhase phase,
        int order,
        Action<ModuleConfigurationContext> configure)
    {
        AddRequest(phase, order, configure);
    }

    internal void AddApplicationBuilderLifecycleRequest(
        ModuleWebStage stage,
        int order,
        Action<ModuleConfigurationContext> configure)
    {
        AddApplicationBuilderRequest(stage, order, configure);
    }

    internal void AddContributionRequest(
        ModulePhase phase,
        int order,
        Action<ModuleConfigurationContext> configure)
    {
        AddRequest(phase, order, configure);
    }

    internal void AddApplicationBuilderContributionRequest(
        ModuleWebStage stage,
        Action<ModuleConfigurationContext> configure)
    {
        AddApplicationBuilderRequest(stage, order: 0, configure);
    }

    private void AddApplicationBuilderRequest(
        ModuleWebStage stage,
        int order,
        Action<ModuleConfigurationContext> configure)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown web lifecycle stage.");
        }

        AddRequest(ModulePhase.ConfigureApplicationBuilder, order, configure, stage);
    }

    private void AddRequest(
        ModulePhase phase,
        int order,
        Action<ModuleConfigurationContext> configure,
        ModuleWebStage? webStage = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var ordinal = _requestOrdinal++;
        _configurationRequests.Add(new ModuleConfigurationRequest(
            phase,
            order,
            ordinal,
            configure,
            webStage));
    }

    internal IReadOnlyList<string> GetMissingRequiredFeatures()
    {
        return _requiredFeatures
            .Where(feature => !_satisfiedFeatures.Contains(feature))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal IReadOnlyList<string> GetUnexpectedSatisfiedFeatures()
    {
        return _satisfiedFeatures
            .Where(feature => !_requiredFeatures.Contains(feature))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Returns lifecycle callbacks in deterministic order.
    /// </summary>
    internal IEnumerable<ModuleConfigurationRequest> GetOrderedRequests(
        IEnumerable<ModuleConfigurationRequest> requests)
    {
        return requests.OrderBy(static request => request.Order)
            .ThenBy(static request => request.Ordinal);
    }

    internal void StartModulePhase(ModulePhase phase)
    {
        Application.Profiling.StartModulePhase(
            ModuleType,
            Application.Dependencies.ResolveModuleKey(ModuleType),
            Order,
            phase);
        ModulePhase = phase;
    }

    internal void EndModulePhase(ModulePhase phase)
    {
        Application.Profiling.StopModulePhase(ModuleType, phase);
    }

    private object CreateBoundOption()
    {
        var option = _optionFactory!();
        if (option is IModuleOptionsContext context)
        {
            context.Bind(Application);
        }

        return option;
    }

    private void EnsureNotFinalized()
    {
        if (IsFinalized)
        {
            throw new InvalidOperationException(
                $"Module {ModuleType.Name} is already compiled and cannot be changed.");
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{ModulePhase} - {ModuleType.Name}";
    }

    private sealed record OptionContribution(
        bool IsHostContribution,
        int Ordinal,
        Action<object> Apply);
}
