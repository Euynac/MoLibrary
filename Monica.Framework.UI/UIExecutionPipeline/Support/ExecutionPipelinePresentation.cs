using Monica.Core.Execution.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIExecutionPipeline.Support;

internal static class ExecutionPipelinePresentation
{
    public static Color GetStatusColor(ExecutionPipelinePlanStatus status) => status switch
    {
        ExecutionPipelinePlanStatus.Building => Color.Info,
        ExecutionPipelinePlanStatus.Ready => Color.Success,
        ExecutionPipelinePlanStatus.Faulted => Color.Error,
        _ => Color.Default
    };

    public static string GetStatusIcon(ExecutionPipelinePlanStatus status) => status switch
    {
        ExecutionPipelinePlanStatus.Building => Icons.Material.Filled.Pending,
        ExecutionPipelinePlanStatus.Ready => Icons.Material.Filled.CheckCircle,
        ExecutionPipelinePlanStatus.Faulted => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.HelpOutline
    };
}
