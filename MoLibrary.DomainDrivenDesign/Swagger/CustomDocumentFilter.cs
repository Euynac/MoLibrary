using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MoLibrary.DomainDrivenDesign.Swagger;

internal class CustomDocumentFilter : IDocumentFilter
{
    //https://stackoverflow.com/questions/55051497/how-to-define-default-values-for-parameters-for-the-swagger-ui
    //TODO POST、GET参数默认值，加速测试
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        //GlobalLog.LogDebug(swaggerDoc.Paths.Select(p => p.Key).StringJoin("\n"));
        var filteredPaths = new List<string>()
        {
            "/", "/api/abp/api-definition", "/api/abp/application-configuration", "/api/abp/application-localization",
            "/api/abp/dapr/event"
        };
        filteredPaths.ForEach(x => { swaggerDoc.Paths.Remove(x); });
    }
}