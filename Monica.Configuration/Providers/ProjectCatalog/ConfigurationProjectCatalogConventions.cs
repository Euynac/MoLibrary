namespace Monica.Configuration.Providers.ProjectCatalog;

internal static partial class ConfigurationProjectCatalogConventions
{
    [System.Text.RegularExpressions.GeneratedRegex("^(.+?)Service\\.")]
    private static partial System.Text.RegularExpressions.Regex DomainPattern();

    public static string GetDomainName(string projectName)
    {
        var match = DomainPattern().Match(projectName);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return "Shared";
    }

    public static string GetProjectDisplayName(string projectName, string domainTitle)
    {
        if (projectName.EndsWith(".Domain"))
        {
            return $"{domainTitle}领域层";
        }

        if (projectName.EndsWith(".Infrastructure"))
        {
            return $"{domainTitle}基础设施层";
        }

        if (projectName.EndsWith(".API"))
        {
            return $"{domainTitle}服务";
        }

        if (projectName == "ProtocolPlatform")
        {
            return "全局微服务配置";
        }

        return projectName;
    }
}
