namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Exception thrown when there are issues with module registration.
/// </summary>
public class ModuleRegisterException(string message) : ModuleException(message)
{
   
}