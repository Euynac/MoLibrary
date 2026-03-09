using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoLogProvider;

namespace Monica.Core.Module.BuilderWrapper;

/// <summary>
/// Manages Harmony patches for intercepting ASP.NET Core lifecycle methods.
/// This allows transparent integration with native ASP.NET Core APIs.
/// </summary>
public static class HarmonyPatchManager
{
    private static readonly object LockObject = new();
    private static bool _isPatched;
    private static bool _patchingFailed;
    private static Harmony? _harmonyInstance;

    private static readonly ILogger Logger = LogProvider.For(typeof(HarmonyPatchManager));

    // Track which instances have been processed to avoid duplicate execution
    internal static readonly ConditionalWeakTable<WebApplicationBuilder, object> ProcessedBuilders = new();
    internal static readonly ConditionalWeakTable<IApplicationBuilder, RoutingState> RoutingStates = new();

    internal class RoutingState
    {
        public bool BeforeRoutingProcessed { get; set; }
        public bool AfterRoutingProcessed { get; set; }
        public bool EndpointsProcessed { get; set; }
    }

    /// <summary>
    /// Ensures Harmony patches are applied exactly once.
    /// Safe to call multiple times - will only patch on first call.
    /// </summary>
    public static void EnsurePatched()
    {
        if (_isPatched || _patchingFailed) return;

        lock (LockObject)
        {
            if (_isPatched || _patchingFailed) return;

            try
            {
                _harmonyInstance = new Harmony("com.monica.aspnetcore.patches");

                // Apply all patches defined in this assembly
                _harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

                _isPatched = true;
                Logger.LogInformation("Harmony patches applied successfully. Native ASP.NET Core methods will trigger module lifecycle events.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex,
                    "Failed to apply Harmony patches. Module system will require explicit calls to MoBuild(), UseMoRouting(), and UseMoEndpoints(). " +
                    "This is not an error if you are using the explicit wrapper methods.");
                _patchingFailed = true;
            }
        }
    }

    /// <summary>
    /// Unpatch all Harmony patches. Used for testing or cleanup.
    /// </summary>
    public static void UnpatchAll()
    {
        lock (LockObject)
        {
            try
            {
                _harmonyInstance?.UnpatchAll("com.monica.aspnetcore.patches");
                _isPatched = false;
                _patchingFailed = false;
                _harmonyInstance = null;

                Logger.LogInformation("All Harmony patches removed.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to remove Harmony patches.");
            }
        }
    }

    /// <summary>
    /// Check if Harmony patches were successfully applied.
    /// </summary>
    public static bool IsPatchingSuccessful => _isPatched && !_patchingFailed;

    internal static RoutingState GetOrCreateRoutingState(IApplicationBuilder app)
    {
        return RoutingStates.GetOrCreateValue(app);
    }
}

#region Harmony Patches

/// <summary>
/// Patches WebApplicationBuilder.Build() to trigger module service registration.
/// </summary>
[HarmonyPatch(typeof(WebApplicationBuilder), nameof(WebApplicationBuilder.Build))]
public static class WebApplicationBuilder_Build_Patch
{
    private static readonly ILogger Logger = LogProvider.For(typeof(WebApplicationBuilder_Build_Patch));

    /// <summary>
    /// Prefix: runs before WebApplicationBuilder.Build()
    /// Triggers BeforeBuild event to register all module services.
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(WebApplicationBuilder __instance)
    {
        try
        {
            // Check if this builder has already been processed
            if (HarmonyPatchManager.ProcessedBuilders.TryGetValue(__instance, out _))
            {
                Logger.LogDebug("WebApplicationBuilder.Build() called again on already-processed instance. Skipping module registration.");
                return;
            }

            // Mark as processed
            HarmonyPatchManager.ProcessedBuilders.Add(__instance, new object());

            Logger.LogDebug("Intercepted WebApplicationBuilder.Build() - triggering module service registration");

            // Trigger the BeforeBuild event via internal method
            WebApplicationBuilderExtensions.TriggerBeforeBuild(__instance);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in WebApplicationBuilder.Build() prefix patch");
            // Re-throw to prevent silent failures
            throw;
        }
    }

    /// <summary>
    /// Postfix: runs after WebApplicationBuilder.Build()
    /// Triggers AfterBuild event for any post-build operations.
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(WebApplication __result)
    {
        try
        {
            Logger.LogDebug("WebApplicationBuilder.Build() completed - triggering post-build operations");

            // Trigger the AfterBuild event via internal method
            WebApplicationBuilderExtensions.TriggerAfterBuild(__result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in WebApplicationBuilder.Build() postfix patch");
            // Re-throw to prevent silent failures
            throw;
        }
    }
}

/// <summary>
/// Patches UseRouting() to trigger middleware configuration before/after routing.
/// </summary>
[HarmonyPatch(typeof(EndpointRoutingApplicationBuilderExtensions), nameof(EndpointRoutingApplicationBuilderExtensions.UseRouting))]
public static class UseRouting_Patch
{
    private static readonly ILogger Logger = LogProvider.For(typeof(UseRouting_Patch));

    /// <summary>
    /// Prefix: runs before UseRouting()
    /// Triggers BeforeUseRouting event to configure middleware with Order &lt;= -1.
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(IApplicationBuilder __0)
    {
        try
        {
            var state = HarmonyPatchManager.GetOrCreateRoutingState(__0);

            if (state.BeforeRoutingProcessed)
            {
                Logger.LogDebug("UseRouting() called again on already-processed instance. Skipping BeforeUseRouting.");
                return;
            }

            state.BeforeRoutingProcessed = true;

            Logger.LogDebug("Intercepted UseRouting() - triggering middleware configuration (before routing)");

            // Trigger the BeforeUseRouting event via internal method
            WebApplicationBuilderExtensions.TriggerBeforeUseRouting(__0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in UseRouting() prefix patch");
            throw;
        }
    }

    /// <summary>
    /// Postfix: runs after UseRouting()
    /// Triggers AfterUseRouting event to configure middleware with Order > -1.
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(IApplicationBuilder __0)
    {
        try
        {
            var state = HarmonyPatchManager.GetOrCreateRoutingState(__0);

            if (state.AfterRoutingProcessed)
            {
                Logger.LogDebug("UseRouting() postfix already processed. Skipping AfterUseRouting.");
                return;
            }

            state.AfterRoutingProcessed = true;

            Logger.LogDebug("UseRouting() completed - triggering middleware configuration (after routing)");

            // Trigger the AfterUseRouting event via internal method
            WebApplicationBuilderExtensions.TriggerAfterUseRouting(__0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in UseRouting() postfix patch");
            throw;
        }
    }
}

/// <summary>
/// Patches UseEndpoints() to trigger endpoint configuration.
/// </summary>
[HarmonyPatch(typeof(EndpointRoutingApplicationBuilderExtensions), nameof(EndpointRoutingApplicationBuilderExtensions.UseEndpoints))]
public static class UseEndpoints_Patch
{
    private static readonly ILogger Logger = LogProvider.For(typeof(UseEndpoints_Patch));

    /// <summary>
    /// Prefix: runs before UseEndpoints()
    /// Triggers BeginUseEndpoints event to configure module endpoints.
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(IApplicationBuilder __0)
    {
        try
        {
            var state = HarmonyPatchManager.GetOrCreateRoutingState(__0);

            if (state.EndpointsProcessed)
            {
                Logger.LogDebug("UseEndpoints() called again on already-processed instance. Skipping BeginUseEndpoints.");
                return;
            }

            state.EndpointsProcessed = true;

            Logger.LogDebug("Intercepted UseEndpoints() - triggering endpoint configuration");

            // Trigger the BeginUseEndpoints event via internal method
            WebApplicationBuilderExtensions.TriggerBeginUseEndpoints(__0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in UseEndpoints() prefix patch");
            throw;
        }
    }
}

#endregion
