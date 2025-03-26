using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.DomainDrivenDesign.AutoController.Settings;

namespace MoLibrary.DomainDrivenDesign.AutoController.Features;

/// <summary>
/// 用于判断哪些需要自动注册为CRUD Controller
/// </summary>
/// <param name="logger"></param>
public class MoConventionalCrudControllerFeatureProvider(ILogger<MoConventionalCrudControllerFeatureProvider> logger, IOptions<MoCrudControllerOption> options) : ControllerFeatureProvider
{
    //private static int SearchTimes = 0;
    protected override bool IsController(TypeInfo typeInfo)
    {
        //SearchTimes++;
        //logger.LogInformation(SearchTimes.ToString());

        if (typeInfo is {IsClass: true, IsGenericType: false} && typeInfo.Name.EndsWith(options.Value.CrudControllerPostfix))
        {
            logger.LogInformation("自动注册为Controller：{name}", typeInfo.Name);
            return true;
        }
        return false;
    }
}
