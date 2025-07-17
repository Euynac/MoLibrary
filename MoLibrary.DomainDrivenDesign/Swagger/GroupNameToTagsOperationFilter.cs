using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MoLibrary.DomainDrivenDesign.Swagger;

/// <summary>
/// 将ApiExplorer.GroupName转换为Swagger Tags的操作过滤器
/// </summary>
internal class GroupNameToTagsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;
        
        if (!string.IsNullOrEmpty(apiDescription.GroupName))
        {
            operation.Tags ??= new List<OpenApiTag>();
            operation.Tags.Clear();
            var controllerName = context.MethodInfo.DeclaringType?.Name ?? "Unknown";
            operation.Tags.Add(new OpenApiTag
            {
                Name = apiDescription.GroupName,
                Description = $"{controllerName} 相关接口"
            });
        }
    }
} 