using System.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Skills.Internal.FileSkill;

internal sealed class MonicaFileSkillScript(
    string name,
    string fullPath,
    AgentFileSkillScriptRunner? runner = null)
    : AgentSkillScript(name)
{
    private readonly AgentFileSkillScriptRunner? _runner = runner;

    internal string FullPath { get; } = fullPath;

    /// <inheritdoc />
    public override async Task<object?> RunAsync(
        AgentSkill skill,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken = default)
    {
        if (skill is not MonicaFileSkill fileSkill)
        {
            throw new InvalidOperationException(
                $"File-based script '{Name}' requires a {nameof(MonicaFileSkill)} but received '{skill.GetType().Name}'.");
        }

        if (_runner is null)
        {
            throw new InvalidOperationException(
                $"Script '{Name}' cannot be executed because no {nameof(AgentFileSkillScriptRunner)} was provided.");
        }

        var runnerScript = CreateFrameworkScript();
        var frameworkSkill = CreateFrameworkSkill(fileSkill);
        return await _runner(frameworkSkill, runnerScript, arguments, cancellationToken).ConfigureAwait(false);
    }

    private AgentFileSkillScript CreateFrameworkScript()
    {
        // Agent Framework exposes the file-skill types but keeps their constructors internal.
        // The public runner delegate requires these exact types, so bridge Monica's public-base wrappers here.
        var script = Activator.CreateInstance(
            typeof(AgentFileSkillScript),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [Name, FullPath, _runner],
            culture: null);

        return script as AgentFileSkillScript
               ?? throw new InvalidOperationException($"Could not create an {nameof(AgentFileSkillScript)} instance.");
    }

    private static AgentFileSkill CreateFrameworkSkill(MonicaFileSkill skill)
    {
        // Keep the runner delegate contract compatible with Agent Framework's file-skill API.
        var frameworkSkill = Activator.CreateInstance(
            typeof(AgentFileSkill),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [skill.Frontmatter, skill.Content, skill.Path, skill.Resources, skill.Scripts],
            culture: null);

        return frameworkSkill as AgentFileSkill
               ?? throw new InvalidOperationException($"Could not create an {nameof(AgentFileSkill)} instance.");
    }
}
