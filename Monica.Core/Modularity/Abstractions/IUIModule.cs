namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Marks a module that owns user-interface composition such as pages, navigation, dialogs, or UI state.
/// </summary>
/// <remarks>
/// UI identity is an explicit capability and is independent of ASP.NET Core middleware or endpoint capability.
/// A UI module implements <see cref="IWebModule"/> separately only when it also contributes to the web lifecycle.
/// </remarks>
public interface IUIModule : IModule
{
}
