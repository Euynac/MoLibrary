using Monica.DomainDrivenDesign.AutoController.MoRpc;

namespace Monica.DomainDrivenDesign.AutoController.Attributes;

/// <summary>
/// Configures generation settings for client-side API callers.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class AutoControllerGeneratorClientConfigAttribute : Attribute
{
    /// <summary>
    /// Whether to generate gRPC implementations.
    /// </summary>
    public bool AddGrpcImplementations { get; set; } = false;
    /// <summary>
    /// Whether to generate HTTP implementations.
    /// </summary>
    public bool AddHttpImplementations { get; set; } = true;

    /// <summary>
    /// HTTP implementation base type. Defaults to <see cref="MoHttpApi"/> when not specified.
    /// This type is used as the generated base class and to resolve the required namespaces.
    /// Custom implementations must inherit from <see cref="MoHttpApi"/> and must not introduce extra constructor parameters.
    /// </summary>
    public Type? HttpImplementationType { get; set; }
}
