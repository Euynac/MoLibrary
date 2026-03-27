using System.Security.Claims;
using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Abstractions;

/// <summary>
/// Perform permission checks
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// Determine whether the current user has the specified permission
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<bool> IsGrantedAsync(string name);

    /// <summary>
    /// Determine whether the provided ClaimsPrincipal has the specified permission
    /// </summary>
    /// <param name="claimsPrincipal"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name);

    /// <summary>
    /// Determine whether the current user has the specified permissions
    /// </summary>
    Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names);

    /// <summary>
    /// Determine whether the provided ClaimsPrincipal has the specified permissions
    /// </summary>
    Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names);
}
