using System.Security.Claims;
using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Abstractions;

/// <summary>
/// 判断授权
/// </summary>
public interface IMoPermissionChecker
{
    /// <summary>
    /// 判断当前用户是否有此权限
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<bool> IsGrantedAsync(string name);

    /// <summary>
    /// 判断给定用户信息是否有此权限
    /// </summary>
    /// <param name="claimsPrincipal"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name);

    /// <summary>
    /// 判断当前用户是否有这些权限
    /// </summary>
    Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names);

    /// <summary>
    /// 判断给定用户信息是否有这些权限
    /// </summary>
    Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names);
}
