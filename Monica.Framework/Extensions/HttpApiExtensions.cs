using Microsoft.AspNetCore.Http.Extensions;
using Monica.Tool.Extensions;

namespace Monica.Framework.Extensions;

public static class HttpApiExtensions
{
    public static string ToQueryString<T>(this T request) where T : class
    {
        var builder = new QueryBuilder();
        foreach (var property in request.GetType().GetProperties().Where(p => p.CanRead))
        {
            var value = property.GetValue(request);
            if (value != null)
            {
                builder.Add(property.Name.ToCamelCase(), value.ToString() ?? "");
            }
        }

        return builder.ToString();
    }
}
