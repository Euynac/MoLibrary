namespace Monica.Core.TypeDiscovery.Models;

/// <summary>
/// Describes an immutable structural query over discovered business types.
/// </summary>
/// <remarks>
/// Queries contain only comparable structural nodes. They deliberately do not accept arbitrary delegates, which lets
/// the module compiler deduplicate equivalent queries and determine the reflection facts needed before scanning.
/// Fluent members combine the current query with an additional requirement by using logical AND.
/// Composite operand order is retained because it also determines the order of open-generic matches exposed to a
/// commit callback; queries with reordered operands are therefore intentionally distinct.
/// </remarks>
public abstract class TypeQuery : IEquatable<TypeQuery>
{
    private static readonly TypeQuery ALL = new ConstantTypeQuery(true);
    private static readonly TypeQuery NONE = new ConstantTypeQuery(false);
    private static readonly TypeQuery CONCRETE_CLASS = new ClassificationTypeQuery(TypeClassification.ConcreteClass);
    private static readonly TypeQuery CLOSED_CLASS = new ClassificationTypeQuery(TypeClassification.ClosedClass);

    /// <summary>
    /// Gets a query that matches every discovered type that is not globally excluded from business-type discovery.
    /// </summary>
    public static TypeQuery All => ALL;

    /// <summary>
    /// Gets a query that matches non-abstract classes. Open generic class definitions are included.
    /// </summary>
    public static TypeQuery ConcreteClass => CONCRETE_CLASS;

    /// <summary>
    /// Gets a query that matches non-abstract classes without unbound generic parameters.
    /// </summary>
    public static TypeQuery ClosedClass => CLOSED_CLASS;

    /// <summary>
    /// Requires the discovered type to be assignable to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="targetType">The target class, interface, delegate, or other runtime type.</param>
    /// <returns>A query containing both the current condition and the assignability condition.</returns>
    public TypeQuery AssignableTo(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        return AllOf(this, new RelatedTypeQuery(RelatedTypeRelation.AssignableTo, targetType));
    }

    /// <summary>
    /// Requires the discovered type to be assignable to <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TTarget">The target runtime type.</typeparam>
    /// <returns>A query containing both the current condition and the assignability condition.</returns>
    public TypeQuery AssignableTo<TTarget>() => AssignableTo(typeof(TTarget));

    /// <summary>
    /// Requires the discovered type to derive from <paramref name="baseType"/>.
    /// </summary>
    /// <param name="baseType">The base class. The base class itself does not match.</param>
    /// <returns>A query containing both the current condition and the subclass condition.</returns>
    public TypeQuery SubclassOf(Type baseType)
    {
        ArgumentNullException.ThrowIfNull(baseType);
        if (!baseType.IsClass)
        {
            throw new ArgumentException("A subclass query requires a class type.", nameof(baseType));
        }

        return AllOf(this, new RelatedTypeQuery(RelatedTypeRelation.SubclassOf, baseType));
    }

    /// <summary>
    /// Requires the discovered type to derive from <typeparamref name="TBase"/>.
    /// </summary>
    /// <typeparam name="TBase">The base class. The base class itself does not match.</typeparam>
    /// <returns>A query containing both the current condition and the subclass condition.</returns>
    public TypeQuery SubclassOf<TBase>() => SubclassOf(typeof(TBase));

    /// <summary>
    /// Requires the discovered type to declare or inherit an attribute compatible with
    /// <paramref name="attributeType"/>.
    /// </summary>
    /// <param name="attributeType">The attribute base or concrete type to locate.</param>
    /// <param name="inherit">
    /// <see langword="true"/> to examine base classes while respecting each attribute's
    /// <see cref="AttributeUsageAttribute.Inherited"/> setting; otherwise, only directly declared attributes are used.
    /// </param>
    /// <returns>A query containing both the current condition and the attribute condition.</returns>
    public TypeQuery HasAttribute(Type attributeType, bool inherit = false)
    {
        ArgumentNullException.ThrowIfNull(attributeType);
        if (!typeof(Attribute).IsAssignableFrom(attributeType))
        {
            throw new ArgumentException("An attribute query requires an Attribute-derived type.", nameof(attributeType));
        }

        return AllOf(this, new AttributeTypeQuery(attributeType, inherit));
    }

    /// <summary>
    /// Requires the discovered type to declare or inherit <typeparamref name="TAttribute"/>.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute base or concrete type to locate.</typeparam>
    /// <param name="inherit">
    /// <see langword="true"/> to examine base classes while respecting attribute inheritance; otherwise,
    /// only directly declared attributes are used.
    /// </param>
    /// <returns>A query containing both the current condition and the attribute condition.</returns>
    public TypeQuery HasAttribute<TAttribute>(bool inherit = false)
        where TAttribute : Attribute => HasAttribute(typeof(TAttribute), inherit);

    /// <summary>
    /// Requires the discovered type to implement at least one closed construction of an open generic interface.
    /// </summary>
    /// <param name="openGenericInterface">An interface type that is an open generic type definition.</param>
    /// <returns>A query containing both the current condition and the open-generic interface condition.</returns>
    /// <remarks>
    /// Matching results retain every corresponding closed interface and its generic arguments so commit code does not
    /// need to call <see cref="Type.GetInterfaces"/> again.
    /// </remarks>
    public TypeQuery ImplementsOpenGeneric(Type openGenericInterface)
    {
        ValidateOpenGenericInterface(openGenericInterface);
        return AllOf(this, new OpenGenericInterfaceTypeQuery(openGenericInterface));
    }

    /// <summary>
    /// Combines this query and <paramref name="other"/> with logical AND.
    /// </summary>
    /// <param name="other">The additional required query.</param>
    /// <returns>The normalized conjunction.</returns>
    public TypeQuery And(TypeQuery other) => AllOf(this, other);

    /// <summary>
    /// Combines this query and <paramref name="other"/> with logical OR.
    /// </summary>
    /// <param name="other">The alternative query.</param>
    /// <returns>The normalized disjunction.</returns>
    public TypeQuery Or(TypeQuery other) => AnyOf(this, other);

    /// <summary>
    /// Negates this query.
    /// </summary>
    /// <returns>A query that matches exactly when this query does not match.</returns>
    public TypeQuery Negate() => Not(this);

    /// <summary>
    /// Creates the logical conjunction of the supplied queries.
    /// </summary>
    /// <param name="queries">The required queries. An empty sequence represents <see cref="All"/>.</param>
    /// <returns>A flattened, structurally deduplicated conjunction that preserves operand order.</returns>
    public static TypeQuery AllOf(params TypeQuery[] queries) => Combine(CompositeOperator.All, queries);

    /// <summary>
    /// Creates the logical disjunction of the supplied queries.
    /// </summary>
    /// <param name="queries">The alternative queries. An empty sequence matches no types.</param>
    /// <returns>A flattened, structurally deduplicated disjunction that preserves operand order.</returns>
    public static TypeQuery AnyOf(params TypeQuery[] queries) => Combine(CompositeOperator.Any, queries);

    /// <summary>
    /// Negates <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The query to negate.</param>
    /// <returns>The normalized logical negation.</returns>
    public static TypeQuery Not(TypeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query switch
        {
            ConstantTypeQuery constant => constant.Value ? NONE : ALL,
            NotTypeQuery not => not.Operand,
            _ => new NotTypeQuery(query)
        };
    }

    /// <inheritdoc />
    public abstract bool Equals(TypeQuery? other);

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) => obj is TypeQuery other && Equals(other);

    /// <inheritdoc />
    public abstract override int GetHashCode();

    internal abstract TypeFactRequirements Requirements { get; }

    internal abstract bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches);

    private static TypeQuery Combine(CompositeOperator @operator, IReadOnlyList<TypeQuery> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var identity = @operator == CompositeOperator.All ? ALL : NONE;
        var absorbing = @operator == CompositeOperator.All ? NONE : ALL;
        var operands = new List<TypeQuery>(queries.Count);
        var seen = new HashSet<TypeQuery>();

        foreach (var query in queries)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (query.Equals(absorbing))
            {
                return absorbing;
            }

            if (query.Equals(identity))
            {
                continue;
            }

            if (query is CompositeTypeQuery composite && composite.Operator == @operator)
            {
                foreach (var nested in composite.Operands)
                {
                    if (seen.Add(nested))
                    {
                        operands.Add(nested);
                    }
                }
            }
            else if (seen.Add(query))
            {
                operands.Add(query);
            }
        }

        return operands.Count switch
        {
            0 => identity,
            1 => operands[0],
            _ => new CompositeTypeQuery(@operator, operands.ToArray())
        };
    }

    private static void ValidateOpenGenericInterface(Type openGenericInterface)
    {
        ArgumentNullException.ThrowIfNull(openGenericInterface);
        if (!openGenericInterface.IsInterface || !openGenericInterface.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                "An open-generic implementation query requires an interface generic type definition.",
                nameof(openGenericInterface));
        }
    }
}

[Flags]
internal enum TypeFactRequirements
{
    None = 0,
    Classification = 1 << 0,
    Assignability = 1 << 1,
    BaseTypes = 1 << 2,
    CustomAttributes = 1 << 3,
    OpenGenericInterfaces = 1 << 4
}
