using System.Text;
using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Internal.FileSkill;

internal sealed class MonicaFileSkillResource(
    string name,
    string fullPath,
    string? description = null,
    string? canonicalName = null)
    : AgentSkillResource(name, description)
{
    internal string FullPath { get; } = fullPath;

    internal string CanonicalName { get; } = string.IsNullOrWhiteSpace(canonicalName)
        ? name
        : canonicalName;

    /// <inheritdoc />
    public override async Task<object?> ReadAsync(
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(FullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }
}
