namespace Monica.UI.Shell.Models;

/// <summary>
/// Stable identifiers for the shared category taxonomy owned by the Monica UI shell.
/// </summary>
public static class BuiltInNavigationCategoryIds
{
    /// <summary>Gets the artificial-intelligence category identifier.</summary>
    public static NavigationCategoryId AI { get; } = NavigationCategoryId.Create("Monica.Navigation.AI");

    /// <summary>Gets the knowledge and retrieval category identifier.</summary>
    public static NavigationCategoryId KnowledgeRetrieval { get; } =
        NavigationCategoryId.Create("Monica.Navigation.KnowledgeRetrieval");

    /// <summary>Gets the documentation category identifier.</summary>
    public static NavigationCategoryId Documentation { get; } =
        NavigationCategoryId.Create("Monica.Navigation.Documentation");

    /// <summary>Gets the configuration category identifier.</summary>
    public static NavigationCategoryId Configuration { get; } =
        NavigationCategoryId.Create("Monica.Navigation.Configuration");

    /// <summary>Gets the infrastructure category identifier.</summary>
    public static NavigationCategoryId Infrastructure { get; } =
        NavigationCategoryId.Create("Monica.Navigation.Infrastructure");

    /// <summary>Gets the module-system category identifier.</summary>
    public static NavigationCategoryId Module { get; } = NavigationCategoryId.Create("Monica.Navigation.Module");

    /// <summary>Gets the task-scheduling category identifier.</summary>
    public static NavigationCategoryId TaskScheduling { get; } =
        NavigationCategoryId.Create("Monica.Navigation.TaskScheduling");

    /// <summary>Gets the monitoring category identifier.</summary>
    public static NavigationCategoryId Monitor { get; } = NavigationCategoryId.Create("Monica.Navigation.Monitor");

    /// <summary>Gets the debugging category identifier.</summary>
    public static NavigationCategoryId Debug { get; } = NavigationCategoryId.Create("Monica.Navigation.Debug");

    /// <summary>Gets the fallback category identifier for navigation entries without an explicit category.</summary>
    public static NavigationCategoryId Uncategorized { get; } =
        NavigationCategoryId.Create("Monica.Navigation.Uncategorized");
}
