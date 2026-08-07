namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Defines stable Skill capability message codes returned by backend services for UI localization.
/// </summary>
public static class SkillCapabilityMessageCode
{
    /// <summary>
    /// Defines disabled-reason codes for Skill catalog entries.
    /// </summary>
    public static class DisabledReason
    {
        /// <summary>
        /// Prefix used when a Skill is unavailable because required modules are missing.
        /// </summary>
        public const string RequiredModulesMissingPrefix = "SkillDisabledReason:" + nameof(RequiredModulesMissing) + ":";

        /// <summary>
        /// The Skill implementation reported that it is not ready, but did not provide a more specific reason.
        /// </summary>
        public const string ImplementationDisabled = "SkillDisabledReason:" + nameof(ImplementationDisabled);

        /// <summary>
        /// The read-only file access Skill has no configured filesystem roots.
        /// </summary>
        public const string ReadOnlyFileAccessNoRoots = "SkillDisabledReason:" + nameof(ReadOnlyFileAccessNoRoots);

        /// <summary>
        /// The Skill catalog is globally disabled.
        /// </summary>
        public const string CatalogDisabled = "SkillDisabledReason:" + nameof(CatalogDisabled);

        /// <summary>
        /// The Skill entry is disabled in runtime capability settings.
        /// </summary>
        public const string EntryDisabled = "SkillDisabledReason:" + nameof(EntryDisabled);

        /// <summary>
        /// Creates a disabled-reason code that includes the missing module type names for display.
        /// </summary>
        /// <param name="missingModules">Missing module strategy types required by the Skill.</param>
        /// <returns>A stable disabled-reason code with a comma-separated module list suffix.</returns>
        public static string RequiredModulesMissing(IEnumerable<Type> missingModules)
        {
            ArgumentNullException.ThrowIfNull(missingModules);

            return RequiredModulesMissingPrefix
                   + string.Join(", ", missingModules.Select(static module => module.Name));
        }

        /// <summary>
        /// Attempts to parse a disabled-reason code created by <see cref="RequiredModulesMissing"/>.
        /// </summary>
        /// <param name="disabledReason">Disabled-reason value returned by the backend.</param>
        /// <param name="moduleList">Comma-separated missing module list when parsing succeeds.</param>
        /// <returns><see langword="true"/> when the reason is a required-module failure.</returns>
        public static bool TryParseRequiredModulesMissing(string disabledReason, out string moduleList)
        {
            ArgumentNullException.ThrowIfNull(disabledReason);

            if (!disabledReason.StartsWith(RequiredModulesMissingPrefix, StringComparison.Ordinal))
            {
                moduleList = string.Empty;
                return false;
            }

            moduleList = disabledReason[RequiredModulesMissingPrefix.Length..];
            return true;
        }
    }
}
