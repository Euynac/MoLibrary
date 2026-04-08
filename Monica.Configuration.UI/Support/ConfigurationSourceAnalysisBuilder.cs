using Monica.Configuration.Models;
using Monica.Configuration.UI.Models;

namespace Monica.Configuration.UI.Support;

public static class ConfigurationSourceAnalysisBuilder
{
    /// <summary>
    /// Builds source-consistency diagnostics for a configuration snapshot.
    /// </summary>
    public static ConfigSourceAnalysis Analyze(ConfigurationSnapshot config)
    {
        var analysis = new ConfigSourceAnalysis
        {
            ConfigName = config.Name,
            ConfigTitle = config.Title
        };

        if (config.Items.Count == 0)
        {
            analysis.IsConsistent = true;
            return analysis;
        }

        var sourceGroups = config.Items
            .GroupBy(item => new { item.Provider, item.Source })
            .ToList();

        analysis.IsConsistent = sourceGroups.Count == 1;
        if (analysis.IsConsistent)
        {
            return analysis;
        }

        foreach (var group in sourceGroups)
        {
            analysis.SourceGroups.Add(new ConfigSourceGroup
            {
                Provider = group.Key.Provider ?? "Unknown",
                Source = group.Key.Source ?? string.Empty,
                Items = group.Select(item => new ConfigItemSourceInfo
                {
                    Key = item.Key,
                    Title = item.Title,
                    Name = item.Name
                }).ToList()
            });
        }

        return analysis;
    }
}
