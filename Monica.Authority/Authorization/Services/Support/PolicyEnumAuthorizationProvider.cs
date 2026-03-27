using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Monica.Authority.Authorization.Abstractions;

namespace Monica.Authority.Authorization.Services.Support;

public class PolicyEnumAuthorizationProvider(
    IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options), IAuthorityAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _options = options.Value;

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);
        if (policy != null)
        {
            return policy;
        }

        //TODO: verify whether this can be converted to an EnumPermission

        var permission = policyName;
        if (permission != null)
        {
            //TODO: Optimize & Cache!
            var policyBuilder = new AuthorizationPolicyBuilder(Array.Empty<string>());
            policyBuilder.Requirements.Add(new PolicyEnumPermissionRequirement(policyName));
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
