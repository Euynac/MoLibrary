using System.Reflection;
using System.Text;
using Monica.EventBus.Constants;
using Monica.Tool.Extensions;

namespace Monica.EventBus.Utils;

/// <summary>
/// Extracts method metadata from delegates.
/// </summary>
internal static class DelegateMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from an event handler delegate.
    /// </summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="handler">Event handler delegate.</param>
    /// <returns>A dictionary containing the extracted metadata.</returns>
    public static Dictionary<string, object> ExtractMetadata<TEvent>(Func<TEvent, Task> handler)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            var method = handler.Method;

            // Extract the method name.
            metadata[SubscriptionMetadataKeys.ActionMethodName] = method.Name;

            // Extract the declaring type.
            if (method.DeclaringType != null)
            {
                metadata[SubscriptionMetadataKeys.ActionDeclaringType] =
                    method.DeclaringType.FullName ?? method.DeclaringType.Name;
            }

            // Build a human-readable method signature.
            var signature = BuildMethodSignature(method);
            metadata[SubscriptionMetadataKeys.ActionMethodSignature] = signature;

            // Record whether the method is static.
            metadata[SubscriptionMetadataKeys.ActionIsStatic] = method.IsStatic;
        }
        catch (Exception)
        {
            // If extraction fails, return whatever metadata has already been collected.
            // This prevents metadata extraction failures from blocking subscription registration.
        }

        return metadata;
    }

    /// <summary>
    /// Builds a human-readable method signature.
    /// </summary>
    /// <param name="method">Method information.</param>
    /// <returns>The formatted method signature.</returns>
    private static string BuildMethodSignature(MethodInfo method)
    {
        var sb = new StringBuilder();

        // Return type.
        sb.Append(method.ReturnType.GetCleanName());
        sb.Append(' ');

        // Method name.
        sb.Append(method.Name);
        sb.Append('(');

        // Parameters.
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(parameters[i].ParameterType.GetCleanName());
            sb.Append(' ');
            sb.Append(parameters[i].Name);
        }

        sb.Append(')');

        return sb.ToString();
    }
}
