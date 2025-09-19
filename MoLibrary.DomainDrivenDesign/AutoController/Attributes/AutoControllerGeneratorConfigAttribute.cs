using System;

namespace MoLibrary.DomainDrivenDesign.AutoController.Attributes;

/// <summary>
/// Configures the AutoController source generator behavior.
/// Can be applied at assembly level to set default configurations for all controllers.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class AutoControllerGeneratorConfigAttribute : Attribute
{
    /// <summary>
    /// The default route prefix to use when no explicit Route attribute is present.
    /// Example: "api/v1"
    /// </summary>
    public string? DefaultRoutePrefix { get; set; }

    /// <summary>
    /// Optional domain name to include in the route pattern.
    /// When specified, routes follow pattern: {DefaultRoutePrefix}/{DomainName}
    /// When not specified, routes use only: {DefaultRoutePrefix}
    /// Example: "Flight", "User", "Order"
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// When true, requires explicit Route attributes on all ApplicationService classes.
    /// When false, allows fallback to default routing patterns.
    /// Default: false
    /// </summary>
    public bool RequireExplicitRoutes { get; set; } = false;
}