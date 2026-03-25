using Microsoft.AspNetCore.Authorization;

namespace Monica.Authority.Authorization.Abstractions;

/// <summary>
/// 对于Policy-based认证进行扩展，作用于Authorize标签
/// </summary>
public interface IAuthorityAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    Task<List<string>> GetPoliciesNamesAsync();
}
