using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Agents.AI;
using Monica.AI.Skills.Internal;
using Monica.Core.Modularity.Models;
using Monica.Core.XmlDocumentation.Abstractions;

namespace Monica.AI.Skills.Abstractions;

/// <summary>
/// Base class for Monica AI skills that expose grouped scripts through Microsoft Agent Skills.
/// </summary>
/// <typeparam name="TSelf">Concrete skill type used for trim-compatible member discovery.</typeparam>
public abstract class MoSkill<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties)]
    TSelf> : AgentClassSkill<TSelf>, IMoSkillMetadata
    where TSelf : MoSkill<TSelf>
{
    private IReadOnlyList<AgentSkillScript>? _moDiscoveredScripts;
    private readonly IXmlDocumentationService? _xmlDocumentationService;

    /// <summary>
    /// Creates a skill without XML documentation enrichment.
    /// </summary>
    protected MoSkill()
    {
    }

    /// <summary>
    /// Creates a skill with XML documentation enrichment for method and parameter descriptions.
    /// </summary>
    /// <param name="xmlDocumentationService">The XML documentation lookup service.</param>
    protected MoSkill(IXmlDocumentationService xmlDocumentationService)
    {
        _xmlDocumentationService = xmlDocumentationService;
    }

    /// <summary>
    /// Module keys that must be loaded for this skill to be available.
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Determines whether this skill is available at startup.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Ordering hint used when multiple skills overlap. Higher priority is listed first.
    /// </summary>
    public virtual int Priority => 0;

    /// <inheritdoc />
    public override IReadOnlyList<AgentSkillScript>? Scripts =>
        _moDiscoveredScripts ??= MoSkillScriptDiscovery.Discover<TSelf>(this, XmlDocumentationService);

    /// <summary>
    /// XML documentation service used to enrich method and parameter descriptions.
    /// </summary>
    protected virtual IXmlDocumentationService? XmlDocumentationService => _xmlDocumentationService;

    internal JsonSerializerOptions? MoSerializerOptions => SerializerOptions;
}
