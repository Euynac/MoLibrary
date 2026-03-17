using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Monica.DomainDrivenDesign.AutoController.Components;

internal sealed class MoTypeCollectionApplicationPart(IEnumerable<Type> types) : ApplicationPart, IApplicationPartTypeProvider
{
    private readonly IReadOnlyList<TypeInfo> _types = types
        .Select(static type => type.GetTypeInfo())
        .ToArray();

    public override string Name => nameof(MoTypeCollectionApplicationPart);

    public IEnumerable<TypeInfo> Types => _types;
}
