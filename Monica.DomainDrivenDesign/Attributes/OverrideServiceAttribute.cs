using Monica.DomainDrivenDesign.AutoCrud;

namespace Monica.DomainDrivenDesign.Attributes;


/// <summary>
/// Marks the method that should eventually be exposed by a service, allowing MoCrudAppService{TEntity,TEntityDto,TKey,TGetListInput,TRepository} and similar application services to override a base signature via `new` or a different parameter list while inheriting the return-type override semantics.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OverrideServiceAttribute(int order = 0) : Attribute
{
    /// <summary>
    /// When multiple members with the same signature exist in the current class or its base classes, the highest order value determines which one appears on the generated interface.
    /// </summary>
    public int Order { get; set; } = order;
}
