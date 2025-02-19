using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocksPlatform.Authority.Authorization;

/// <summary>
/// 对于Policy-based认证进行扩展，作用于Authorize标签
/// </summary>
public interface IMoAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    Task<List<string>> GetPoliciesNamesAsync();
}
