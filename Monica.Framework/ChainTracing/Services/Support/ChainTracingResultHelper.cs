using Microsoft.AspNetCore.Mvc;

namespace Monica.Framework.ChainTracing.Services.Support;

public static class ChainTracingResultHelper
{
    /// <summary>
    /// Gets the effective response type name.
    /// </summary>
    /// <param name="type">The declared return type.</param>
    /// <returns>The innermost generic type name, or the type name itself.</returns>
    public static string GetResponseTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericArg = type.GenericTypeArguments.FirstOrDefault();
            while (genericArg?.IsGenericType == true)
            {
                genericArg = genericArg.GenericTypeArguments.FirstOrDefault();
            }
            return genericArg?.Name ?? type.Name;
        }
        return type.Name;
    }
    /// <summary>
    /// Extracts the underlying result object from an <see cref="IActionResult" />.
    /// </summary>
    /// <param name="result">The action result.</param>
    /// <returns>The extracted payload, or the original result when no wrapper is recognized.</returns>
    public static object? ExtractResult(IActionResult? result)
    {
        return result switch
        {
            ObjectResult objectResult => objectResult.Value,
            JsonResult jsonResult => jsonResult.Value,
            ContentResult contentResult => contentResult.Content,
            _ => result
        };
    }
}
