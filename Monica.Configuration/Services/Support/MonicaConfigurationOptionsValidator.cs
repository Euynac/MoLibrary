using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Bridges Monica runtime configuration validation into Microsoft.Extensions.Options.
/// </summary>
/// <typeparam name="TOptions">The managed options type.</typeparam>
internal sealed class MonicaConfigurationOptionsValidator<TOptions>(
    IConfigurationRuntimeValidationService validationService,
    string definitionKey)
    : IValidateOptions<TOptions>
    where TOptions : class
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (name is not null && !string.Equals(name, Options.DefaultName, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        var report = validationService.GetReport(definitionKey);
        return report.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(report.Issues.Select(ConfigurationRuntimeValidationMessageFormatter.FormatOptionsIssue));
    }
}
