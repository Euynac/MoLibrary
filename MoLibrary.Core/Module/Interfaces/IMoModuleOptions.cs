namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleControllerOption<TModule> : IMoModuleControllerOption, IMoModuleOption<TModule> where TModule : IMoModule
{
    public string? SwaggerGroupName { get; set; }

    public string RoutePrefix { get; set; } = nameof(TModule);
    public string SwaggerTag { get; set; } = nameof(TModule);
    public bool EnableControllers { get; set; }
    public string GetSwaggerGroupName() => SwaggerGroupName ?? nameof(TModule);
}
/// <summary>
/// Defines the options for a module in the MoLibrary.
/// </summary>
public interface IMoModuleControllerOption
{
    public string? SwaggerGroupName { get; set; }
    /// <summary>
    /// Gets or sets the route prefix for the module.
    /// </summary>
    /// <value>
    /// The route prefix used for the module's endpoints.
    /// </value>
    public string RoutePrefix { get; set; }

    /// <summary>
    /// Gets or sets the Swagger tag for the module.
    /// </summary>
    /// <value>
    /// The tag used in Swagger documentation for the module.
    /// </value>
    public string SwaggerTag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether controllers are enabled for the module.
    /// </summary>
    /// <value>
    /// <c>true</c> if controllers are enabled; otherwise, <c>false</c>.
    /// </value>
    public bool EnableControllers { get; set; }
}
