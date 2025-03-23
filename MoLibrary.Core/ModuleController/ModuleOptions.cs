namespace MoLibrary.Core.ModuleController;

/// <summary>
/// Defines the options for a module in the MoLibrary.
/// </summary>
public interface IMoModuleOptions
{
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
