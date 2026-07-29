using Microsoft.Extensions.Options;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Marker options used to run host-level Monica composition validation before hosted services start.
/// </summary>
internal sealed class ModuleCompositionStartupOptions;

/// <summary>
/// Validates that ASP.NET Core hosts completed both Monica application-builder stages.
/// </summary>
internal sealed class ModuleCompositionStartupValidator(MonicaApplication application)
    : IValidateOptions<ModuleCompositionStartupOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ModuleCompositionStartupOptions options)
    {
        var failure = application.Modules.GetStartupValidationFailure();
        return failure is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
    }
}
