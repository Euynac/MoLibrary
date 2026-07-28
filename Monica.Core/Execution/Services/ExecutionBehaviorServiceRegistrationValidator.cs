using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Execution.Models.Internal;

namespace Monica.Core.Execution.Services;

internal sealed class ExecutionBehaviorServiceRegistrationValidator
{
    public ExecutionBehaviorServiceRegistrationValidator(
        IReadOnlyList<ExecutionBehaviorRegistration> registrations,
        IServiceCollection services)
    {
        foreach (var registration in registrations)
        {
            registration.ValidateExclusiveServiceRegistration(services);
        }
    }
}
