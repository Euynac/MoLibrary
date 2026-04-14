namespace Monica.WebApi.AutoControllers.Services.Support;

/// <summary>
/// Stores the controller types that AutoControllers should expose through MVC application parts.
/// This state is owned by the module instance so direct registration and transitive registration
/// share the same runtime behavior.
/// </summary>
internal sealed class AutoControllerApplicationPartCatalog
{
    private readonly HashSet<Type> _applicationPartTypes = [];

    public void Add(Type type)
    {
        _applicationPartTypes.Add(type);
    }

    public IReadOnlyCollection<Type> GetApplicationPartTypes()
    {
        return _applicationPartTypes;
    }
}
