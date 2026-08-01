using Microsoft.Extensions.Options;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Marker options used to validate composition and required startup work before hosted services start.
/// </summary>
internal sealed class ModuleStartupValidationOptions;

/// <summary>
/// Validates Web composition and releases the required host-lifecycle startup-work barrier.
/// </summary>
internal sealed class ModuleStartupValidator(MonicaApplication application)
    : IValidateOptions<ModuleStartupValidationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ModuleStartupValidationOptions options)
    {
        var failure = application.Modules.GetStartupValidationFailure();
        return failure is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
    }
}
