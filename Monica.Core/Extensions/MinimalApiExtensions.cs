using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Monica.Tool.Extensions;

namespace Monica.Core.Extensions;

/// <summary>
/// Provides a shared endpoint-filter workaround because ASP.NET Core does not support global endpoint filters yet.
/// See aspnetcore issue <c>#43237</c>.
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
}
