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
            operation.Tags.Clear(); //默认情况下如果不自己打Tag，会有一个默认的Tag，值和Controller名相同。
            var controllerName = context.MethodInfo.DeclaringType?.Name ?? "Unknown";
            operation.Tags.Add(new OpenApiTag
            {
                Name = apiDescription.GroupName,
                Description = $"{controllerName} 相关接口"
            });
        }
    }
} 