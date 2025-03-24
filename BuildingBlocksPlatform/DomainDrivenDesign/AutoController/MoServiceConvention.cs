using BuildingBlocksPlatform.DomainDrivenDesign.Attributes;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Features;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Interfaces;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Settings;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud.Interfaces;
using BuildingBlocksPlatform.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.Tool.Utils;


namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController;

public class MoServiceConvention(
    IMoConventionalRouteBuilder conventionalRouteBuilder, ILogger<MoServiceConvention> logger, IOptions<MoAutoControllerOption> options)
    : IMoServiceConvention
{
    public ILogger<MoServiceConvention> Logger => logger;

    protected IMoConventionalRouteBuilder ConventionalRouteBuilder { get; } = conventionalRouteBuilder;

    public void Apply(ApplicationModel application)
    {
        ApplyForControllers(application);
    }
    protected virtual void ApplyForControllers(ApplicationModel application)
    {
        foreach (var controller in GetControllers(application))
        {
            var controllerType = controller.ControllerType.AsType();

            controller.ControllerName = controller.ControllerName.RemovePostFix(MoAutoControllerOption.AutoControllerPostfix);

            ConfigureRemoteService(controller, new ConventionalControllerSetting());
        }
    }


    protected virtual IList<ControllerModel> GetControllers(ApplicationModel application)
    {
        return application.Controllers;
    }

    protected virtual void ConfigureRemoteService(ControllerModel controller, ConventionalControllerSetting? configuration)
    {
        ConfigureApiExplorer(controller);
        ConfigureSelector(controller, configuration);
        ConfigureParameters(controller);
    }
    #region 对Controller方法Api显示及Aciton进行整体修正

    protected virtual void ConfigureApiExplorer(ControllerModel controller)
    {
        if (string.IsNullOrEmpty(controller.ApiExplorer.GroupName))
        {
            controller.ApiExplorer.GroupName = controller.ControllerName;
        }

        var name = controller.DisplayName;

        if (controller.ApiExplorer.IsVisible is not false)
        {
            RemoveDisableAction(controller.Actions, controller.ControllerType.GetInterface(nameof(IMoCrudDisableDelete)) != null);
            RemoveDuplicateActon(controller.Actions);
        }

        foreach (var action in controller.Actions)
        {
            ConfigureApiExplorer(action);
        }
    }
    protected virtual void ConfigureApiExplorer(ActionModel action)
    {
        if (action.ApiExplorer.IsVisible != null)
        {
            return;
        }
    }


    private static void RemoveDisableAction(ICollection<ActionModel> actionModels, bool disableDelete)
    {
        var removeList = new List<ActionModel>();
        foreach (var actionModel in actionModels)
        {
            if (actionModel.Parameters.Any(a => a.ParameterInfo.ParameterType == typeof(OurCrudDisableDto)))
            {
                removeList.Add(actionModel);
            }
            else if (actionModel.ActionName.Contains("Delete") && disableDelete)
            {
                removeList.Add(actionModel);
            }
        }


        foreach (var actionModel in removeList)
        {
            actionModels.Remove(actionModel);
        }
    }

    private static void RemoveDuplicateActon(ICollection<ActionModel> actionModels)
    {
        var removeList = new List<ActionModel>();
        foreach (var grouping in actionModels.GroupBy(p => p.ActionName).Where(p => p.Count() > 1))
        {
            removeList.AddRange(grouping.OrderByDescending(p =>
                ((OverrideServiceAttribute?) p.Attributes.FirstOrDefault(a =>
                    a.GetType() == typeof(OverrideServiceAttribute)))?.Order ?? int.MinValue).Skip(1));
        }

        foreach (var actionModel in removeList)
        {
            actionModels.Remove(actionModel);
        }

        //var hashSet = actionModels.GroupBy(action => action.ActionName).Where(p => p.Count() > 1).Select(p => p.Key)
        //    .ToHashSet();
        //if (hashSet.Count > 0)
        //{
        //    actionModels.RemoveAll(actionModel =>
        //        hashSet.Contains(actionModel.ActionName) &&
        //        actionModel.Attributes.All(p => p.GetType() != typeof(OverrideServiceAttribute))
        //    );
        //}

    }

    #endregion

   

    #region 对Controller的方法参数进行修正，如添加FromBody特性
    protected virtual void ConfigureParameters(ControllerModel controller)
    {
        /* Default binding system of Asp.Net Core for a parameter
         * 1. Form values
         * 2. Route values.
         * 3. Query string.
         */

        foreach (var action in controller.Actions)
        {
            foreach (var prm in action.Parameters)
            {
                if (prm.BindingInfo != null)
                {
                    continue;
                }
                if (!TypeHelper.IsPrimitiveExtended(prm.ParameterInfo.ParameterType, includeEnums: true))
                {
                    if (CanUseFormBodyBinding(action, prm))
                    {
                        prm.BindingInfo = BindingInfo.GetBindingInfo(new[] { new FromBodyAttribute() });
                    }
                }
            }
        }
    }

    protected virtual bool CanUseFormBodyBinding(ActionModel action, ParameterModel parameter)
    {
        //We want to use "id" as path parameter, not body!
        if (parameter.ParameterName == "id")
        {
            return false;
        }

        foreach (var selector in action.Selectors)
        {
            foreach (var actionConstraint in selector.ActionConstraints)
            {
                var httpMethodActionConstraint = actionConstraint as HttpMethodActionConstraint;
                if (httpMethodActionConstraint == null)
                {
                    continue;
                }

                if (httpMethodActionConstraint.HttpMethods.All(hm => hm.IsIn("GET", "DELETE", "TRACE", "HEAD")))
                {
                    return false;
                }
            }
        }

        return true;
    }


    #endregion


    #region 对Controller及其Action的Selector进行配置

    protected virtual void ConfigureSelector(ControllerModel controller, ConventionalControllerSetting? configuration)
    {
        RemoveEmptySelectors(controller.Selectors);

        //这部分是过滤掉不需要自动生成的Controller基类的，ASP.NET Core会添加AttributeRouteModel 即打上了[Route]标签的
        if (controller.Selectors.Any(selector => selector.AttributeRouteModel != null))
        {
            return;
        }

        //配置接口route前缀path
        var rootPath = options.Value.RoutePath;

        foreach (var action in controller.Actions)
        {
            ConfigureSelector(rootPath, controller.ControllerName, action, configuration);
        }
    }

    protected virtual void ConfigureSelector(string rootPath, string controllerName, ActionModel action, ConventionalControllerSetting? configuration)
    {
        RemoveEmptySelectors(action.Selectors);

        if (!action.Selectors.Any())
        {
            AddMoServiceSelector(rootPath, controllerName, action, configuration);
        }
        else
        {
            NormalizeSelectorRoutes(rootPath, controllerName, action, configuration);
        }
    }

    protected virtual void AddMoServiceSelector(string rootPath, string controllerName, ActionModel action, ConventionalControllerSetting? configuration)
    {
        var httpMethod = SelectHttpMethod(action, configuration);

        var abpServiceSelectorModel = new SelectorModel
        {
            AttributeRouteModel = CreateMoServiceAttributeRouteModel(rootPath, controllerName, action, httpMethod, configuration),
            ActionConstraints = { new HttpMethodActionConstraint([httpMethod]) }
        };

        action.Selectors.Add(abpServiceSelectorModel);
    }

    protected virtual void NormalizeSelectorRoutes(string rootPath, string controllerName, ActionModel action, ConventionalControllerSetting? configuration)
    {
        foreach (var selector in action.Selectors)
        {
            var httpMethod = selector.ActionConstraints
                .OfType<HttpMethodActionConstraint>()
                .FirstOrDefault()?
                .HttpMethods?
                .FirstOrDefault() ?? SelectHttpMethod(action, configuration);

            selector.AttributeRouteModel ??= CreateMoServiceAttributeRouteModel(rootPath, controllerName, action, httpMethod, configuration);

            if (!selector.ActionConstraints.OfType<HttpMethodActionConstraint>().Any())
            {
                selector.ActionConstraints.Add(new HttpMethodActionConstraint([httpMethod]));
            }
        }
    }

    /// <summary>
    /// 根据Action类名自动判断HttpMethod
    /// </summary>
    /// <param name="action"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    protected virtual string SelectHttpMethod(ActionModel action, ConventionalControllerSetting? configuration)
    {
        return HttpMethodHelper.GetConventionalVerbForMethodName(action.ActionName);
    }

    /// <summary>
    /// 自动生成Route信息设置
    /// </summary>
    /// <param name="rootPath"></param>
    /// <param name="controllerName"></param>
    /// <param name="action"></param>
    /// <param name="httpMethod"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    protected virtual AttributeRouteModel CreateMoServiceAttributeRouteModel(string rootPath, string controllerName, ActionModel action, string httpMethod, ConventionalControllerSetting? configuration)
    {
        return new AttributeRouteModel(
            new RouteAttribute(
                ConventionalRouteBuilder.Build(rootPath, controllerName, action, httpMethod, configuration)
            )
        );
    }

    protected virtual void RemoveEmptySelectors(IList<SelectorModel> selectors)
    {
        selectors
            .Where(IsEmptySelector)
            .ToList()
            .ForEach(s => selectors.Remove(s));
    }

    protected virtual bool IsEmptySelector(SelectorModel selector)
    {
        return selector.AttributeRouteModel == null
               && selector.ActionConstraints.IsNullOrEmptySet()
               && selector.EndpointMetadata.IsNullOrEmptySet();
    }
    #endregion




}
