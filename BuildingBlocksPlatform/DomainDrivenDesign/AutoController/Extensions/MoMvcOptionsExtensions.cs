using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Extensions;

internal static class MoMvcOptionsExtensions
{
    //https://stackoverflow.com/a/54148525  巨坑：service.Configure支持叠加
    public static void ConfigMoMvcOptions(this MvcOptions options, IServiceCollection services)
    {
        AddConventions(options, services);
        AddActionFilters(options);
        AddPageFilters(options);
        AddModelBinders(options);
        AddMetadataProviders(options, services);
        AddFormatters(options);
    }

    private static void AddFormatters(MvcOptions options)
    {
        //options.OutputFormatters.Insert(0, new RemoteStreamContentOutputFormatter());
    }

    private static void AddConventions(MvcOptions options, IServiceCollection services)
    {
        options.Conventions.Add(new MoServiceConventionWrapper(services.BuildServiceProvider()));
    }

    private static void AddActionFilters(MvcOptions options)
    {
        //options.Filters.AddService(typeof(GlobalFeatureActionFilter));
        //options.Filters.AddService(typeof(AbpAuditActionFilter));
        //options.Filters.AddService(typeof(AbpNoContentActionFilter));
        //options.Filters.AddService(typeof(AbpFeatureActionFilter));
        //options.Filters.AddService(typeof(AbpValidationActionFilter));
        //options.Filters.AddService(typeof(MoActionFilterUow));
        options.Filters.AddService(typeof(MoResultFilterMvc));
        //options.Filters.AddService(typeof(ExceptionFilter));
    }

    private static void AddPageFilters(MvcOptions options)
    {
        //options.Filters.AddService(typeof(GlobalFeaturePageFilter));
        //options.Filters.AddService(typeof(ExceptionPageFilter));
        //options.Filters.AddService(typeof(AbpAuditPageFilter));
        //options.Filters.AddService(typeof(AbpFeaturePageFilter));
        //options.Filters.AddService(typeof(AbpUowPageFilter));
    }

    private static void AddModelBinders(MvcOptions options)
    {
        //options.ModelBinderProviders.Insert(0, new AbpDateTimeModelBinderProvider());
        //options.ModelBinderProviders.Insert(1, new AbpExtraPropertiesDictionaryModelBinderProvider());
        //options.ModelBinderProviders.Insert(2, new AbpRemoteStreamContentModelBinderProvider());
    }

    private static void AddMetadataProviders(MvcOptions options, IServiceCollection services)
    {
        //options.ModelMetadataDetailsProviders.Add(new AbpDataAnnotationAutoLocalizationMetadataDetailsProvider(services));

        //options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(IRemoteStreamContent), BindingSource.FormFile));
        //options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(IEnumerable<IRemoteStreamContent>), BindingSource.FormFile));
        //options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(RemoteStreamContent), BindingSource.FormFile));
        //options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(IEnumerable<RemoteStreamContent>), BindingSource.FormFile));
        //options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(IRemoteStreamContent)));
        //options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(RemoteStreamContent)));
    }
}
