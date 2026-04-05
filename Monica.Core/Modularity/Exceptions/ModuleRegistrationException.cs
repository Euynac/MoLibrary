namespace Monica.Core.Modularity.Exceptions;

/// <summary>
/// Exception thrown when there are issues with module registration.
/// </summary>
public class ModuleRegistrationException(string message) : ModuleException(message)
{
   
}