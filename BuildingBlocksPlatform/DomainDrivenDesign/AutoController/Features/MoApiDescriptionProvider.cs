using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Extensions;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud;
using BuildingBlocksPlatform.SeedWork;
using Koubot.Tool.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Features;

//仅需注册，ASP.NET Core会自动发现所有已注册的Provider进行处理。
public class MoApiDescriptionProvider(IModelMetadataProvider modelMetadataProvider,
        IOptions<MvcOptions> mvcOptionsAccessor)
    : IApiDescriptionProvider, ITransientDependency
{
    private readonly MvcOptions _mvcOptions = mvcOptionsAccessor.Value;
    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
    }

    /// <summary>
    /// The order -999 ensures that this provider is executed right after the
    /// Microsoft.AspNetCore.Mvc.ApiExplorer.DefaultApiDescriptionProvider.
    /// </summary>
    public int Order => -999;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
        //TODO 增加Open API响应描述

        //IOptions<AbpRemoteServiceApiDescriptionProviderOptions> optionsAccessor

        //AbpAspNetCoreMvcModule.cs
        //Configure<AbpRemoteServiceApiDescriptionProviderOptions>(options =>
        //{
        //    var statusCodes = new List<int>
        //    {
        //        (int) HttpStatusCode.Forbidden,
        //        (int) HttpStatusCode.Unauthorized,
        //        (int) HttpStatusCode.BadRequest,
        //        (int) HttpStatusCode.NotFound,
        //        (int) HttpStatusCode.NotImplemented,
        //        (int) HttpStatusCode.InternalServerError
        //    };

        //    options.SupportedResponseTypes.AddIfNotContains(statusCodes.Select(statusCode => new ApiResponseType
        //    {
        //        Type = typeof(RemoteServiceErrorResponse),
        //        StatusCode = statusCode
        //    }));
        //});



        //foreach (var apiResponseType in GetApiResponseTypes())
        //{
        //    foreach (var result in context.Results.Where(x => x.IsRemoteService()))
        //    {
        //        var actionProducesResponseTypeAttributes =
        //            ReflectionHelper.GetAttributesOfMemberOrDeclaringType<ProducesResponseTypeAttribute>(
        //                result.ActionDescriptor.GetMethodInfo());
        //        if (actionProducesResponseTypeAttributes.Any(x => x.StatusCode == apiResponseType.StatusCode))
        //        {
        //            continue;
        //        }

        //        result.SupportedResponseTypes.AddIfNotContains(x => x.StatusCode == apiResponseType.StatusCode,
        //            () => apiResponseType);
        //    }
        //}

        UpdateDynamicListResponseTypeToExactType(context);
        return;
    }

    /// <summary>
    /// 使得CRUD Dynamic类型的接口也能在swagger上显示出具体的Dto类型
    /// </summary>
    /// <param name="context"></param>
    protected virtual void UpdateDynamicListResponseTypeToExactType(ApiDescriptionProviderContext context)
    {
        foreach (var apiDescription in context.Results.Where(p=>p.ActionDescriptor.IsControllerAction()))
        {
            var controllerActionDescriptor = apiDescription.ActionDescriptor.AsControllerActionDescriptor();
            if (!controllerActionDescriptor.ActionName.EqualsAny("List", "GetList"))
            {
                continue;
            }
            var type = controllerActionDescriptor.ControllerTypeInfo;
            if (!type.ImplementedInterfaces.Contains(typeof(IMoCrudAppService)))
            {
                continue;
            }

            var finalBaseType = type.BaseType;
            while (finalBaseType is not null)
            {
                if (finalBaseType.IsDerivedFromGenericType(typeof(MoCrudAppService<,,,,,,,,>)))
                {
                    break;
                }

                finalBaseType = finalBaseType.BaseType;
            }
            if(finalBaseType is null) continue;

            if (finalBaseType.GenericTypeArguments.Length == 8 && apiDescription.SupportedResponseTypes.FirstOrDefault() is {StatusCode: 200} responseType)
            {
                var getListOutputDto = finalBaseType.GenericTypeArguments[2];
                responseType.Type = typeof(Res<>).MakeGenericType(getListOutputDto);
                responseType.ModelMetadata = modelMetadataProvider.GetMetadataForType(responseType.Type);

            }
        }
    }

    //protected virtual IEnumerable<ApiResponseType> GetApiResponseTypes()
    //{
    //    foreach (var apiResponse in _options.SupportedResponseTypes)
    //    {
    //        apiResponse.ModelMetadata = modelMetadataProvider.GetMetadataForType(apiResponse.Type!);

    //        foreach (var responseTypeMetadataProvider in _mvcOptions.OutputFormatters.OfType<IApiResponseTypeMetadataProvider>())
    //        {
    //            var formatterSupportedContentTypes = responseTypeMetadataProvider.GetSupportedContentTypes(null!, apiResponse.Type!);
    //            if (formatterSupportedContentTypes == null)
    //            {
    //                continue;
    //            }

    //            foreach (var formatterSupportedContentType in formatterSupportedContentTypes)
    //            {
    //                apiResponse.ApiResponseFormats.Add(new ApiResponseFormat
    //                {
    //                    Formatter = (IOutputFormatter)responseTypeMetadataProvider,
    //                    MediaType = formatterSupportedContentType
    //                });
    //            }
    //        }
    //    }

    //    return _options.SupportedResponseTypes;
    //}
}