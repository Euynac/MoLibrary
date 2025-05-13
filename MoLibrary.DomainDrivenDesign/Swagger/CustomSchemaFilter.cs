using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MoLibrary.DomainDrivenDesign.Swagger;

/// <summary>
/// 用于去除Swagger中Response多余的code和message属性
/// </summary>
internal class CustomSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        //for each schema name start with "Response", fetch its properties, and remove all properties name "code" and "message"
        if (context.Type.Name.StartsWith("Response"))
        {
            var properties = schema.Properties;
            var code = properties.FirstOrDefault(x => x.Key == "code");
            var message = properties.FirstOrDefault(x => x.Key == "message");
            var innovation = properties.FirstOrDefault(x => x.Key == "invocationChain");
            if (code.Key != null)
            {
                properties.Remove(code.Key);
            }

            if (message.Key != null)
            {
                properties.Remove(message.Key);
            }

            if (innovation.Key != null)
            {
                properties.Remove(innovation.Key);
            }
        }
    }
}