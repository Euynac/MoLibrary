using System.Diagnostics.CodeAnalysis;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocksPlatform.Extensions;

/// <summary>
/// 2024-10-25目前不支持全局Endpoint Filter设置，因此只能出此下策。 详见aspnetcore issues: #43237
/// </summary>
public static class MinimalApiExtensions
{
    private static Action<RouteHandlerBuilder>? _sharedFilterAction;
    /// <summary>
    /// Registers a filter of type <typeparamref name="TFilterType" /> onto the route handler.
    /// </summary>
    /// <typeparam name="TFilterType">The type of the <see cref="T:Microsoft.AspNetCore.Http.IEndpointFilter" /> to register.</typeparam>
    /// <returns>A <see cref="T:Microsoft.AspNetCore.Builder.RouteHandlerBuilder" /> that can be used to further customize the route handler.</returns>
    public static void AddSharedEndpointFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFilterType>()
        where TFilterType : IEndpointFilter
    {
        ActionExtensions.WrapAction(ref _sharedFilterAction, handlerBuilder => handlerBuilder.AddEndpointFilter<RouteHandlerBuilder, TFilterType>());
    }
    
    /// <summary>
    /// Registers shared filter of type onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="T:Microsoft.AspNetCore.Builder.RouteHandlerBuilder" />.</param>
    /// <returns>A <see cref="T:Microsoft.AspNetCore.Builder.RouteHandlerBuilder" /> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder UseSharedEndpointFilter(
        this RouteHandlerBuilder builder)
    {
        _sharedFilterAction?.Invoke(builder);
        return builder;
    }

    /// <summary>
    /// 封装为Minimal API response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <returns></returns>
    public static IResult GetResponse<T>(this T response) where T : IServiceResponse
    {
        return Results.Json(response, statusCode: (int?) response.GetHttpStatusCode());
    }

}