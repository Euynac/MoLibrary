using BuildingBlocksPlatform.Authority.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BuildingBlocksPlatform.Authority.Implements.Authorization;

public class MoEnumAuthorizationPolicyProvider(
    IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options), IMoAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _options = options.Value;

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);
        if (policy != null)
        {
            return policy;
        }

        //TODO 验证是否能够转换为EnumPermission

        var permission = policyName;
        if (permission != null)
        {
            //TODO: Optimize & Cache!
            var policyBuilder = new AuthorizationPolicyBuilder(Array.Empty<string>());
            policyBuilder.Requirements.Add(new EnumPermissionRequirement(policyName));
            return policyBuilder.Build();
        }

        return null;
    }

    public async Task<List<string>> GetPoliciesNamesAsync()
    {
        throw new NotImplementedException();
        //return _options.GetPoliciesNames()
        //    .Union(
        //        (await permissionDefinitionManager
        //            .GetPermissionsAsync())
        //        .Select(p => p.Name)
        //    )
        //    .ToList();
    }
}
