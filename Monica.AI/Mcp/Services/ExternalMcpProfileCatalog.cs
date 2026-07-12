using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Services;

internal sealed class ExternalMcpProfileCatalog(
    IEnumerable<ExternalMcpClientProfile> codeProfiles,
    IExternalMcpClientProfileStore profileStore)
{
    private readonly IReadOnlyList<ExternalMcpClientProfile> _codeProfiles = codeProfiles
        .Select(static profile => profile.Normalize(ExternalMcpClientProfileOrigin.Code))
        .ToList();

    internal async Task<IReadOnlyList<ExternalMcpClientProfile>> GetProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var userProfiles = await profileStore.LoadAsync(cancellationToken);
        var profiles = _codeProfiles
            .Concat(userProfiles)
            .OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ValidateUniqueNames(profiles);
        return profiles;
    }

    private static void ValidateUniqueNames(IReadOnlyList<ExternalMcpClientProfile> profiles)
    {
        var duplicateNames = profiles
            .GroupBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate MCP client names are not allowed: " + string.Join(", ", duplicateNames) + ".");
        }
    }
}
