namespace MoLibrary.Core.Module.Interfaces;

/// <summary>
/// Defines the options for a module in the MoLibrary.
/// </summary>
public interface IMoModuleControllerOption
{
    /// <summary>
    /// Gets or sets the Swagger tag for the module.
    /// </summary>
    /// <value>
    /// The tag used in Swagger documentation for the module.
    /// </value>
    public string? SwaggerTag { get; set; }
    /// <summary>
    /// Gets or sets the route prefix for the module.
    /// </summary>
    /// <value>
    /// The route prefix used for the module's endpoints.
    /// </value>
    public string RoutePrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether controllers are enabled for the module.
    /// </summary>
    /// <value>
    /// <c>true</c> if controllers are enabled; otherwise, <c>false</c>.
    /// </value>
    public bool DisableControllers { get; set; }

    /// <summary>
    /// If <c>true</c>, <c>APIExplorer.ApiDescription</c> objects will be created for the associated
    /// controller or action.
    /// </summary>
    /// <remarks>
    /// Set this value to configure whether the associated controller or action will appear in ApiExplorer.
    /// </remarks>
    bool? IsVisibleInSwagger { get; set; }

    /// <summary>
    /// Gets the Swagger group name for the module.
    /// </summary>
    /// <returns></returns>
    public string GetSwaggerGroupName();
}
