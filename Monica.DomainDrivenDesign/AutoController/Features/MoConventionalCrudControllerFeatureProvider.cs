using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DomainDrivenDesign.AutoController.Settings;
using Monica.DomainDrivenDesign.AutoCrud;
using Monica.Tool.Extensions;

namespace Monica.DomainDrivenDesign.AutoController.Features;

/// <summary>
/// Determines which application services should be exposed as generated CRUD controllers.
/// </summary>
/// <param name="logger">Logger used to report controller registration decisions.</param>
/// <param name="options">CRUD controller options.</param>
public class MoConventionalCrudControllerFeatureProvider(ILogger<MoConventionalCrudControllerFeatureProvider> logger, IOptions<MoCrudControllerOption> options) : ControllerFeatureProvider
{
    //private static int SearchTimes = 0;
    protected override bool IsController(TypeInfo typeInfo)
    {
        //SearchTimes++;
        //logger.LogInformation(SearchTimes.ToString());

        if (typeInfo is {IsClass: true, IsGenericType: false} && typeInfo.AsType().IsImplementInterface<IMoCrudAppService>())
        {
            if (typeInfo.Name.EndsWith(options.Value.CrudControllerPostfix))
            {
                logger.LogInformation("自动注册 CRUD Controller：{name}", typeInfo.Name);
            }
            else
            {
                logger.LogError("自动注册 CRUD Controller：{name} 失败，因为与要求后缀「{postfix}」不匹配", typeInfo.Name, options.Value.CrudControllerPostfix);
            }
            return true;
        }
        return false;
    }
}
