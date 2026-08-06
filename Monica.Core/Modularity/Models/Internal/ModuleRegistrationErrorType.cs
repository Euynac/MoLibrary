namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Defines the types of errors that can occur during module registration.
/// </summary>
internal enum ModuleRegistrationErrorType
{
    /// <summary>
    /// A general error with no specific type.
    /// </summary>
    General = 0,

    /// <summary>
    /// Error raised by required scheduled module startup work.
    /// </summary>
    StartupWorkError = 1,

    /// <summary>
    /// Error raised while the serial composition thread publishes a completed startup-work result.
    /// </summary>
    StartupWorkCommitError = 2
}
