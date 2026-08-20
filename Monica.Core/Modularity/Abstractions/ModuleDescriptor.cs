namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Declares the option-free graph contract of one module.
/// </summary>
/// <remarks>
/// Descriptors are valid only while the module graph is being compiled. They do not expose module options,
/// services, or host state, so graph shape cannot depend on configuration timing.
/// </remarks>
public sealed class ModuleDescriptor
{
    private readonly MonicaApplication _application;
    private readonly Type _ownerModuleType;

    internal ModuleDescriptor(MonicaApplication application, Type ownerModuleType)
    {
        _application = application;
        _ownerModuleType = ownerModuleType;
    }

    /// <summary>
    /// Adds a hard dependency. The dependency is included in the composition automatically.
    /// </summary>
    public void Require<TModule, TOptions>(Action<TOptions>? configure = null)
        where TModule : MonicaModule<TOptions>, new()
        where TOptions : ModuleOptions<TModule>, new()
    {
        _application.Modules.DescribeRequire<TModule, TOptions>(_ownerModuleType, configure);
    }

    /// <summary>
    /// Declares a feature that must be selected on a directly required dependency.
    /// </summary>
    /// <typeparam name="TModule">The directly required dependency module.</typeparam>
    /// <typeparam name="TOptions">The dependency's option type.</typeparam>
    /// <param name="featureName">The stable dependency feature name used in validation diagnostics.</param>
    /// <remarks>
    /// Call <see cref="Require{TModule,TOptions}(Action{TOptions}?)"/> first. This narrow operation preserves the
    /// graph-only descriptor boundary while allowing an intrinsic consumer to require a provider capability owned
    /// by its dependency.
    /// </remarks>
    public void RequireDependencyFeature<TModule, TOptions>(string featureName)
        where TModule : MonicaModule<TOptions>, new()
        where TOptions : ModuleOptions<TModule>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        _application.Modules.DescribeRequireDependencyFeature<TModule, TOptions>(
            _ownerModuleType,
            featureName);
    }

    /// <summary>
    /// Adds an ordering relationship without including the target module.
    /// </summary>
    public void AfterIfPresent<TModule, TOptions>()
        where TModule : MonicaModule<TOptions>, new()
        where TOptions : ModuleOptions<TModule>, new()
    {
        _application.Modules.DescribeAfterIfPresent(_ownerModuleType, typeof(TModule));
    }

    /// <summary>
    /// Declares that an explicit registration feature must be selected before composition can proceed.
    /// </summary>
    /// <param name="featureName">The stable feature name used in validation diagnostics.</param>
    public void RequireFeature(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        _application.Modules.DescribeRequireFeature(_ownerModuleType, featureName);
    }
}
