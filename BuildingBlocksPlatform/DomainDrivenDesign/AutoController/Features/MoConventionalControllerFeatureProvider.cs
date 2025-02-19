using System.Reflection;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Settings;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Features;

/// <summary>
/// 用于判断哪些需要注册为Controller
/// </summary>
/// <param name="logger"></param>
public class MoConventionalControllerFeatureProvider(ILogger<MoConventionalControllerFeatureProvider> logger) : ControllerFeatureProvider
{
    //private static int SearchTimes = 0;
    protected override bool IsController(TypeInfo typeInfo)
    {
        //SearchTimes++;
        //logger.LogInformation(SearchTimes.ToString());

        if (typeInfo is {IsClass: true, IsGenericType: false} && typeInfo.Name.EndsWith(MoAutoControllerOption.AutoControllerPostfix))
        {
            logger.LogInformation("自动注册为Controller：{name}", typeInfo.Name);
            return true;
        }
        return false;
    }
}
