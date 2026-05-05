namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Defines stable MCP capability message codes returned by backend services for UI localization.
/// </summary>
public static class McpCapabilityMessageCode
{
    /// <summary>
    /// Defines connectivity test result codes.
    /// </summary>
    public static class Connectivity
    {
        /// <summary>
        /// The MCP endpoint was reached successfully.
        /// </summary>
        public const string ConnectedToEndpoint = nameof(ConnectedToEndpoint);

        /// <summary>
        /// The MCP endpoint was reached and tool listing completed successfully.
        /// </summary>
        public const string ConnectedAndListedTools = nameof(ConnectedAndListedTools);

        /// <summary>
        /// A local HTTP MCP server is available in-process, but no absolute display URL can be probed.
        /// </summary>
        public const string LocalHttpDisplayUrlMissing = nameof(LocalHttpDisplayUrlMissing);

        /// <summary>
        /// A local stdio MCP server is available in-process.
        /// </summary>
        public const string LocalStdioInProcessReady = nameof(LocalStdioInProcessReady);
    }

    /// <summary>
    /// Defines disabled-reason codes for MCP catalog entries.
    /// </summary>
    public static class DisabledReason
    {
        /// <summary>
        /// The skill-backed MCP server is disabled by persisted skill MCP exposure settings.
        /// </summary>
        public const string SkillMcpExposureDisabled = "DisabledReason:" + nameof(SkillMcpExposureDisabled);

        /// <summary>
        /// The MCP entry is generated from a skill and is intentionally not exposed again as Monica-agent MCP tools.
        /// </summary>
        public const string SkillServerNotAgentTool = "DisabledReason:" + nameof(SkillServerNotAgentTool);

        /// <summary>
        /// The MCP entry is not configured to expose tools to Monica agents.
        /// </summary>
        public const string NotAgentTool = "DisabledReason:" + nameof(NotAgentTool);

        /// <summary>
        /// The MCP catalog is globally disabled.
        /// </summary>
        public const string CatalogDisabled = "DisabledReason:" + nameof(CatalogDisabled);

        /// <summary>
        /// The MCP entry is disabled in runtime capability settings.
        /// </summary>
        public const string EntryDisabled = "DisabledReason:" + nameof(EntryDisabled);
    }
}
