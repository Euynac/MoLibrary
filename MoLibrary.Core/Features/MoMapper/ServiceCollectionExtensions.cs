using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MoLibrary.Framework.Extensions;

namespace MoLibrary.Core.Features.MoMapper;

public static class ServiceCollectionExtensions
{
    public static void AddMoMapper(this IServiceCollection services, Action<MoMapperOption>? action = null)
    {
     
        var option = new MoMapperOption();
        action?.Invoke(option);
        if (option.DebugMapper)
        {
            //https://github.com/MapsterMapper/Mapster/wiki/Debugging
            MapperExtensions.EnableMapperDebugging();
        }

        Task.Factory.StartNew(() =>
        {
            TypeAdapterConfig.GlobalSettings.Compile();
        }).ContinueWith((t) =>
        {
            Environment.FailFast($"Mapper����ʧ�ܣ������������顣{t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);

        services.AddSingleton(TypeAdapterConfig.GlobalSettings);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddTransient<IMoMapper, MapsterProviderMoObjectMapper>();
    }
}


public class MoMapperOption
{
    public ILogger Logger { get; set; } = NullLogger.Instance;
    /// <summary>
    /// ���ö�Mapper���е��ԣ���ʱ��֧���ֶ����ԣ�
    /// </summary>
    public bool DebugMapper { get; set; } = true;
}
