using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;
using MoLibrary.DomainDrivenDesign.Swagger;
using MoLibrary.Tool.Extensions;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;

namespace MoLibrary.DomainDrivenDesign.Modules;

public class ModuleSwagger(ModuleSwaggerOption option) : MoModule<ModuleSwagger, ModuleSwaggerOption, ModuleSwaggerGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Swagger;
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint($"/swagger/{option.Version}/swagger.json",
                $"{option.AppName ?? "Unknown"} {option.Version}");
            c.DocumentTitle = option.AppName ?? "Swagger UI";
            //c.RoutePrefix = string.Empty; // 设置根路径访问Swagger UI
            //c.InjectStylesheet("/swagger-ui/custom.css"); // 可选：自定义样式表
            //c.InjectJavascript("/swagger-ui/custom.js"); // 可选：自定义JavaScript
        });
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints => { endpoints.MapGet("/", () => Results.LocalRedirect("~/swagger")); });
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.DocumentFilter<CustomDocumentFilter>();
            options.SchemaFilter<CustomSchemaFilter>();
            options.SwaggerDoc(option.Version, new OpenApiInfo
            {
                Title = option.AppName,
                Version = option.Version,
                Description = option.Description ?? ""
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

            var documentAssemblies = (option.DocumentAssemblies ?? []).ToList();
            documentAssemblies.Add(typeof(MoCrudPageRequestDto).Assembly.GetName().Name!);
            documentAssemblies.Add(typeof(IHasRequestFilter).Assembly.GetName().Name!);
            if (!option.DisableAutoIncludeEntryAsDocumentAssembly)
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
                    Logger.LogWarning($"Swagger XML file not found: {filePath}");
                }
            }
            //似乎必须写在IncludeXmlComments下面，而且只支持/// <inheritdoc /> 一行
            options.IncludeXmlCommentsFromInheritDocs(includeRemarks: true, excludedTypes: typeof(string));//扩展支持inherit doc

            if (option.UseAuth)
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

            option.ExtendSwaggerGenAction?.Invoke(options);

        }); //https://github.com/domaindrivendev/Swashbuckle.AspNetCore#include-descriptions-from-xml-comments
    }
}