using Microsoft.AspNetCore.Authorization;

namespace Monica.Authority.Authorization.Abstractions;

/// <summary>
/// Extend policy-based authorization for use with the Authorize attribute
/// </summary>
public interface IAuthorityAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    Task<List<string>> GetPoliciesNamesAsync();
}
