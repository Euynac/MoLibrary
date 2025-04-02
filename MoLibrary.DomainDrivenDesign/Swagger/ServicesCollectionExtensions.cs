using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Core.Extensions;
using MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;
using MoLibrary.Logging;
using MoLibrary.Tool.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;

namespace MoLibrary.DomainDrivenDesign.Swagger;

public static class ServicesCollectionExtensions
{
    /// <summary>
    /// 注册 Swagger 文档服务
    /// </summary>
    public static void AddMoSwagger(this IServiceCollection services, Action<MoSwaggerConfig>? configAction = null, Action<SwaggerGenOptions>? extendSwaggerGenOptions = null)
    {
        services.ConfigActionWrapper(configAction, out var swaggerConfig);
        services.AddSwaggerGen(options =>
        {
            extendSwaggerGenOptions?.Invoke(options);
            options.DocumentFilter<CustomDocumentFilter>();
            options.SchemaFilter<CustomSchemaFilter>();
            options.SwaggerDoc(swaggerConfig.Version, new OpenApiInfo
            {
                Title = swaggerConfig.AppName,
                Version = swaggerConfig.Version,
                Description = swaggerConfig.Description ?? ""
            });
            options.AddEnumsWithValuesFixFilters();//扩展支持Enum

            //巨坑： 这个方法其实是swagger右上角分组时判断是否显示的。但是如果不调用，会导致ABP(以及自己定义的CrudAutoController约定生成的)生成的所有的接口都不显示。
            options.DocInclusionPredicate((docName, description) => true);

            //https://github.com/swagger-api/swagger-ui/issues/7911
            //https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet/issues/285

            //巨坑：默认情况下，Swagger使用类型的全名来生成schemaId，但如果遇到匿名类型，可能会因为类型名称不稳定或者重复而产生冲突。特别是在返回匿名类型的多个方法中，如果结构相同但Swagger认为它们不同，可能会生成相同的schemaId，从而导致冲突
            //巨坑：对于非法字符生成也会出现问题，所以需要过滤非法字符。.Replace('+', '.') 似乎不需要.Replace("`", "_")
            //最佳的方案当然是返回值均采用显式DTO类型
            options.CustomSchemaIds(type => type.GetCleanFullName());

            //巨坑：要显示swagger文档，需要设置项目<GenerateDocumentationFile>True</GenerateDocumentationFile> XML文档用于生成swagger api注释。另外还要在设置中指定xml文档地址

            var documentAssemblies = (swaggerConfig.DocumentAssemblies ?? []).ToList();
            documentAssemblies.Add(typeof(MoCrudPageRequestDto).Assembly.GetName().Name!);
            documentAssemblies.Add(typeof(IHasRequestFilter).Assembly.GetName().Name!);
            if (!swaggerConfig.DisableAutoIncludeEntryAsDocumentAssembly)
            {
                documentAssemblies.Add(Assembly.GetEntryAssembly()!.GetName().Name!);
            }

            foreach (var name in documentAssemblies.Distinct())
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