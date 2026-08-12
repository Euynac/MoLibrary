using Monica.Tool.Extensions;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.WebApi.AutoControllers.Services.Support;

/// <summary>
/// Describes a generated CRUD controller whose CLR name cannot be stripped by the configured route suffix.
/// </summary>
internal sealed record CrudControllerSuffixMismatch(
    string ControllerName,
    string ControllerType,
    string RequiredSuffix)
{
    internal const string SOURCE = "AutoControllers.CrudControllerSuffix";

    /// <summary>
    /// Creates a diagnostic only for participating CRUD services whose name violates a non-empty suffix.
    /// </summary>
    internal static CrudControllerSuffixMismatch? Create(Type type, string? requiredSuffix)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsImplementInterface<ICrudApplicationService>()
               && !string.IsNullOrEmpty(requiredSuffix)
               && !type.Name.EndsWith(requiredSuffix, StringComparison.Ordinal)
            ? new CrudControllerSuffixMismatch(
                type.Name,
                type.GetCleanFullName(),
                requiredSuffix)
            : null;
    }
}
