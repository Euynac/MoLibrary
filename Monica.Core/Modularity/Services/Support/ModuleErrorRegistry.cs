using System.Text;
using Monica.Core.Extensions;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Records fatal errors produced after the immutable module graph has been compiled.
/// </summary>
internal sealed class ModuleErrorRegistry(MonicaApplication application)
{
    /// <summary>
    /// Records a required startup-work failure.
    /// </summary>
    internal void RecordStartupWorkError(ModuleStartupWorkResult result)
    {
        if (result.Failure is null)
        {
            throw new ArgumentException("A successful startup work result cannot be recorded as an error.", nameof(result));
        }

        application.Modules.AddRegistrationError(new ModuleRegistrationError
        {
            ModuleType = result.ModuleType,
            ErrorMessage =
                $"Error in startup work '{result.Name}' ({result.Barrier}): " +
                result.Failure.GetMessageRecursively(),
            ErrorType = ModuleRegistrationErrorType.StartupWorkError,
            Phase = result.OriginPhase,
            StackTrace = result.Failure.StackTrace
        });
    }

    /// <summary>
    /// Records a failure raised while the serial composition thread publishes completed startup-work state.
    /// </summary>
    internal void RecordStartupWorkCommitError(
        ModuleStartupWorkResult result,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        application.Modules.AddRegistrationError(new ModuleRegistrationError
        {
            ModuleType = result.ModuleType,
            ErrorMessage =
                $"Error committing startup work '{result.Name}' ({result.Barrier}): " +
                exception.GetMessageRecursively(),
            ErrorType = ModuleRegistrationErrorType.StartupWorkError,
            Phase = result.OriginPhase,
            StackTrace = exception.StackTrace
        });
    }

    /// <summary>
    /// Aborts composition when any error has been recorded. Compiled modules are never disabled as error recovery.
    /// </summary>
    internal void RaiseModuleErrors()
    {
        if (application.Modules.RegistrationErrors.Count == 0)
        {
            return;
        }

        throw new ModuleRegistrationException(BuildErrorMessage(application.Modules.RegistrationErrors));
    }

    private static string BuildErrorMessage(IReadOnlyList<ModuleRegistrationError> errors)
    {
        var message = new StringBuilder("Module registration errors:");
        foreach (var group in errors.GroupBy(static error => error.ModuleType))
        {
            message.AppendLine();
            message.AppendLine($"Module {group.Key.Name}:");
            foreach (var error in group)
            {
                message.AppendLine(error.ToString());
            }
        }

        return message.ToString();
    }
}
