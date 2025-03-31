using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Tool.MoResponse;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

namespace MoLibrary.Core.Features.MoMapper;

public static class ServiceCollectionExtensions
{
    private static bool _hasInit;//TODO ����תΪModule�Զ��жϡ�ע����Ҫ���ֿ����ߵ��ú��ڲ�module����ע������ȼ������⻹��Ҫ֧���ڲ�module��������Option��������뿪�����趨Ҫ�ϲ�����

    public static void AddMoMapper(this IServiceCollection services, Action<MoMapperOption>? action = null)
    {
        if(_hasInit)return;
        _hasInit = true;
        var option = new MoMapperOption();
        action?.Invoke(option);
        
        if (option.DebugMapper)
        {
            //https://github.com/MapsterMapper/Mapster/wiki/Debugging
            TypeAdapterConfig.GlobalSettings.Compiler = exp => exp.CompileWithDebugInfo(
                new ExpressionCompilationOptions()
                {
                    //ThrowOnFailedCompilation = true,
                    EmitFile = true,
                    References = [Assembly.GetAssembly(typeof(Res))!, Assembly.GetAssembly(typeof(Enumerable))!, .. option.DebuggerRelatedAssemblies ?? []]
                });
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

    /// <summary>
    /// Mapper״̬�м��
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoMapper(this IApplicationBuilder app, string? groupName = "Mapper")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "Mapper��ؽӿ�" }
            };
            endpoints.MapGet("/mapper/status", async (HttpResponse response, HttpContext context) =>
            {
                var cards = MapperExtensions.GetInfos();
                var res = new
                {
                    count = cards.Count,
                    cards = cards.Select(x => new
                    {
                        x.SourceType,
                        x.DestinationType,
                        x.MapExpression
                    })
                };
                await context.Response.WriteAsJsonAsync(res);
            }).WithName("��ȡMapper״̬��Ϣ").WithOpenApi(operation =>
            {
                operation.Summary = "��ȡMapper״̬��Ϣ";
                operation.Description = "��ȡMapper״̬��Ϣ";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}


public class MoMapperOption
{
    public ILogger Logger { get; set; } = NullLogger.Instance;
    /// <summary>
    /// ���ö�Mapper���е��ԣ���ʱ��֧���ֶ����ԣ�
    /// </summary>
    public bool DebugMapper { get; set; } = false;

    /// <summary>
    /// ������Ҫ����Mapper����ʱ�漰�Ļ������չ������ض���ĳ���
    /// </summary>
    public Assembly[]? DebuggerRelatedAssemblies { get; set; }
}
