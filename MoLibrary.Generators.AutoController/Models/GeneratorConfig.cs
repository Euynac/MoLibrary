namespace MoLibrary.Generators.AutoController.Models;

internal class GeneratorConfig
{
    public string? DefaultRoutePrefix { get; set; }
    public string? DomainName { get; set; }
    public bool RequireExplicitRoutes { get; set; } = false;
    public bool SkipGeneration { get; set; } = false;

    public static GeneratorConfig Default => new()
    {
        DefaultRoutePrefix = null,
        DomainName = null,
        RequireExplicitRoutes = true,
        SkipGeneration = false
    };

    public bool HasDefaultRouting => !string.IsNullOrEmpty(DefaultRoutePrefix) && !RequireExplicitRoutes;
}