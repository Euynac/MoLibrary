using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;

namespace MoLibrary.DomainDrivenDesign.Swagger;

public static class ServicesCollectionExtensions
{
    /// <summary>
    /// 注册 Swagger 文档服务
    /// </summary>
    public static void AddMoSwagger(this IServiceCollection services, Action<MoSwaggerConfig>? configAction = null)
    {
        services.ConfigActionWrapper(configAction, out var swaggerConfig);
        services.AddSwaggerGen(options =>
        {
            options.DocumentFilter<CustomDocumentFilter>();
            options.SchemaFilter<CustomSchemaFilter>();
            options.SwaggerDoc(swaggerConfig.Version, new OpenApiInfo
            {
                Title = swaggerConfig.AppName,
                Version = swaggerConfig.Version,
                Description = swaggerConfig.Description ?? ""
            });
            options.AddEnumsWithValuesFixFilters();//扩展支持Enum

            //巨坑： 这个方法其实是swagger右上角分组时判断是否显示的。但是如果不调用，会导致ABP生成的所有的接口都不显示。
            options.DocInclusionPredicate((docName, description) => true);

            //https://github.com/swagger-api/swagger-ui/issues/7911
            //https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet/issues/285
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));

            //巨坑：要显示swagger文档，需要设置项目<GenerateDocumentationFile>True</GenerateDocumentationFile> XML文档用于生成swagger api注释。另外还要在设置中指定xml文档地址
            if (swaggerConfig.DocumentAssemblies is { } names)
            {
                foreach (var name in names)
                {
                    var filePath = Path.Combine(AppContext.BaseDirectory, $"{name}.xml");
                    if (File.Exists(filePath))
                    {
                        options.IncludeXmlComments(filePath);
                    }
                    else
                    {
                        GlobalLog.LogWarning($"Swagger XML file not found: {filePath}");
                    }
                }
            }
            //似乎必须写在IncludeXmlComments下面，而且只支持/// <inheritdoc /> 一行
            options.IncludeXmlCommentsFromInheritDocs(includeRemarks: true, excludedTypes: typeof(string));//扩展支持inherit doc

            if (swaggerConfig.UseAuth)
            {
                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "JWT Authentication",
                    Description = "Enter JWT Bearer token **_only_**",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer", // must be lowercase
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference
                    {
                        Id = JwtBearerDefaults.AuthenticationScheme,
                        Type = ReferenceType.SecurityScheme
                    }
                };
                options.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {securityScheme, Array.Empty<string>()}
                });
            }


        }); //https://github.com/domaindrivendev/Swashbuckle.AspNetCore#include-descriptions-from-xml-comments
    }

    /// <summary>
    /// 使用 Swagger 中间件
    /// </summary>
    public static void UseMoEndpointsSwagger(this IApplicationBuilder app)
    {
        //获取MoSwaggerConfig
        var config = app.ApplicationServices.GetService<IOptions<MoSwaggerConfig>>()?.Value;

        if (config is null)
        {
            GlobalLog.LogError("SwaggerConfig is null, Swagger will not be registered.");
            return;
        }

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint($"/swagger/{config.Version}/swagger.json",
                $"{config.AppName ?? "Unknown"} {config.Version}");
        });
        app.UseEndpoints(endpoints => { endpoints.MapGet("/", () => Results.LocalRedirect("~/swagger")); });
    }
}

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

//https://stackoverflow.com/questions/55051497/how-to-define-default-values-for-parameters-for-the-swagger-ui
//TODO POST、GET参数默认值，加速测试

internal class CustomDocumentFilter : IDocumentFilter
{
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