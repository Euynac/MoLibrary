using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DomainDrivenDesign.Swagger;
using Monica.Tool.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.Swagger)]
public class ModuleSwagger(ModuleSwaggerOption option) : MoModule<ModuleSwagger, ModuleSwaggerOption, ModuleSwaggerGuide>(option)
{

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint($"/swagger/{Option.Version}/swagger.json",
                $"{Option.AppName ?? "Unknown"} {Option.Version}");
            c.DocumentTitle = Option.AppName ?? "Swagger UI";
            c.RoutePrefix = Option.RoutePrefix;

            // Allow other modules to extend SwaggerUI configuration
            Option.ExtendSwaggerUIAction?.Invoke(c);
        });
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // // Add a filter that maps ApiExplorer.GroupName values to Swagger tags.
            // options.OperationFilter<GroupNameToTagsOperationFilter>();
            
            options.SwaggerDoc(Option.Version, new OpenApiInfo
            {
                Title = Option.AppName,
                Version = Option.Version,
                Description = Option.Description ?? ""
            });
            options.AddEnumDocumentation();

            // Pitfall: this API guards the visibility toggle in the Swagger UI's top-right group picker, and if omitted ABP (and our CrudAutoController-generated endpoints) will hide every controller.
            options.DocInclusionPredicate((docName, description) => true);

            //https://github.com/swagger-api/swagger-ui/issues/7911
            //https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet/issues/285

            // Pitfall: Swagger uses a type's full name for schema IDs by default, so anonymous types with unstable or duplicated names can collide when multiple methods return structurally identical payloads.
            // Pitfall: illegal characters also crash schema generation, so we filter them out. Replacing '+' with '.' is necessary; `.Replace("`", "_")` appears redundant.
            // Best practice is to expose explicit DTO types instead of anonymous results.
            options.CustomSchemaIds(type => type.GetCleanFullName());

            if(!Option.DisableXmlDocumentation)
            {
                // Pitfall: to generate Swagger docs you must enable `<GenerateDocumentationFile>True</GenerateDocumentationFile>` in each project and supply the resulting XML documents.
                var documentAssemblies = (Option.DocumentAssemblies ?? []).ToList();
                if (!Option.DisableAutoIncludeModuleSystemRelatedAsDocumentAssembly)
                {
                    documentAssemblies.AddRange(Mo.Options.GlobalTypeFinder.GetAssemblies().Select(p => p.GetName().Name!));
                }

                var xmlFilePaths = new List<string>();
                foreach (var name in documentAssemblies.Distinct())
                {
                    var filePath = Path.Combine(AppContext.BaseDirectory, $"{name}.xml");
                    if (File.Exists(filePath))
                    {
                        xmlFilePaths.Add(filePath);
                    }
                    else if (!name.StartsWith(nameof(Monica)))
                    {
                        Logger.LogWarning($"Swagger XML file not found: {filePath}, you need to add <GenerateDocumentationFile>True</GenerateDocumentationFile> into your .csproj file to generate swagger documents");
                    }
                }

                if (!Option.DisableInheritDocFilter)
                {
                    options.IncludeXmlCommentsWithInheritDoc(xmlFilePaths, includeControllerXmlComments: true, Logger);
                }
                else
                {
                    foreach (var filePath in xmlFilePaths)
                    {
                        options.IncludeXmlComments(filePath);
                    }
                }
            }
          

            if (Option.UseAuth)
            {
                ConfigureJwtBearerSecurity(options);
            }

            Option.ExtendSwaggerGenAction?.Invoke(options);

        }); //https://github.com/domaindrivendev/Swashbuckle.AspNetCore#include-descriptions-from-xml-comments
    }

    private const string JwtSecuritySchemeId = JwtBearerDefaults.AuthenticationScheme;
    private const string JwtBearerHttpScheme = "bearer";

    private static void ConfigureJwtBearerSecurity(SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(JwtSecuritySchemeId, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter JWT Bearer token **_only_**",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerHttpScheme,
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(JwtSecuritySchemeId, document)] = []
        });
    }

}

public static class ModuleSwaggerBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Swagger module.
        /// </summary>
        public static ModuleSwaggerGuide AddSwagger(Action<ModuleSwaggerOption>? action = null)
        {
            return new ModuleSwaggerGuide().Register(action);
        }
    }
}

public class ModuleSwaggerGuide : MoModuleGuide<ModuleSwagger, ModuleSwaggerOption, ModuleSwaggerGuide>
{
}

public class ModuleSwaggerOption : MoModuleOption<ModuleSwagger>
{
    public Action<SwaggerGenOptions>? ExtendSwaggerGenAction { get; set; }

    /// <summary>
    /// Callback to extend Swagger UI configuration, allowing other modules (such as the SwaggerUI module) to customize its behavior.
    /// </summary>
    public Action<SwaggerUIOptions>? ExtendSwaggerUIAction { get; set; }

    /// <summary>
    /// Application name.
    /// </summary>
    public string? AppName { get; set; } = "ApplicationName";

    /// <summary>
    /// API version.
    /// </summary>
    public string? Version { get; set; } = "v1";

    /// <summary>
    /// Document description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Disable Swagger's automatic XML documentation loading.
    /// </summary>
    public bool DisableXmlDocumentation { get; set; }

    /// <summary>
    /// Names of the service projects whose XML documentation should be picked up for Swagger; each project must set `<GenerateDocumentationFile>True</GenerateDocumentationFile>`.
    /// </summary>
    public string[]? DocumentAssemblies { get; set; }

    /// <summary>
    /// Disable automatically adding module-system-related assemblies to the Swagger generation pipeline.
    /// </summary>
    public bool DisableAutoIncludeModuleSystemRelatedAsDocumentAssembly { get; set; }
    
    

    /// <summary>
    /// Require authentication for Swagger UI.
    /// </summary>
    public bool UseAuth { get; set; }

    /// <summary>
    /// Disable the inheritdoc filter that imports XML comments from referenced methods.
    /// </summary>
    public bool DisableInheritDocFilter { get; set; }

    /// <summary>
    /// Swagger UI route prefix (defaults to "swagger"); set to an empty string to expose Swagger at the application root.
    /// </summary>
    public string RoutePrefix { get; set; } = "swagger";
}
