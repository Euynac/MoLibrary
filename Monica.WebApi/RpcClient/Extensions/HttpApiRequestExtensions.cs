using Microsoft.AspNetCore.Http.Extensions;
using Monica.Tool.Extensions;

namespace Monica.WebApi.RpcClient.Extensions;

/// <summary>
/// Provides request serialization helpers used by generated HTTP RPC clients.
/// </summary>
public static class HttpApiRequestExtensions
{
    /// <summary>
    /// Serializes readable request properties into an escaped query string.
    /// Null values are omitted and property names are converted to camel case.
    /// </summary>
    /// <typeparam name="TRequest">The request model type.</typeparam>
    /// <param name="request">The request model to serialize.</param>
    /// <returns>An escaped query string, including the leading question mark when values are present.</returns>
    public static string ToQueryString<TRequest>(this TRequest request)
        where TRequest : class
    {
        var builder = new QueryBuilder();
        foreach (var property in request.GetType().GetProperties().Where(static property => property.CanRead))
        {
            var value = property.GetValue(request);
            if (value is not null)
            {
                builder.Add(property.Name.ToCamelCase(), value.ToString() ?? string.Empty);
            }
        }

        return builder.ToString();
    }
}
