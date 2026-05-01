using System.Text;
using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Internal.FileSkill;

internal sealed class MonicaFileSkillResource(string name, string fullPath) : AgentSkillResource(name)
{
    internal string FullPath { get; } = fullPath;

    /// <inheritdoc />
    public override async Task<object?> ReadAsync(
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(FullPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }
}
