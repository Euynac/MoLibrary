using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Monica.WebApi.AutoControllers.Services.Support;

internal sealed class TypeCollectionApplicationPart(IEnumerable<Type> types) : ApplicationPart, IApplicationPartTypeProvider
{
    private readonly IReadOnlyList<TypeInfo> _types = types
        .Select(static type => type.GetTypeInfo())
        .ToArray();

    public override string Name => nameof(TypeCollectionApplicationPart);

    public IEnumerable<TypeInfo> Types => _types;
}
