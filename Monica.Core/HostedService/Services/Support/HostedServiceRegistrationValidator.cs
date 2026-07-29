using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Monica.Core.HostedService.Services.Support;

internal sealed class HostedServiceRegistrationValidationOptions;

/// <summary>
/// Rejects hosted-service registrations whose lifetime cannot preserve the instance observed by the Generic Host.
/// </summary>
internal sealed class HostedServiceRegistrationValidator(HostedServiceDescriptorCatalog descriptorCatalog)
    : IValidateOptions<HostedServiceRegistrationValidationOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        HostedServiceRegistrationValidationOptions options)
    {
        var invalidDescriptors = descriptorCatalog.GetDescriptors()
            .Where(static descriptor => descriptor.Lifetime != ServiceLifetime.Singleton)
            .Select(Describe)
            .ToArray();

        return invalidDescriptors.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Every IHostedService registration must be singleton so lifecycle observers and the Generic Host use "
                + $"the same instance. Invalid registrations: {string.Join("; ", invalidDescriptors)}.");
    }

    private static string Describe(ServiceDescriptor descriptor)
    {
        var key = descriptor.IsKeyedService
            ? $", key '{descriptor.ServiceKey}'"
            : string.Empty;
        return $"{typeof(IHostedService).FullName} ({descriptor.Lifetime}{key})";
    }
}
