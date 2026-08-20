using System.Linq.Expressions;

namespace Monica.StateStore.Queries;

/// <summary>
/// Stores the typed query shape until a provider renders its own wire protocol.
/// </summary>
/// <param name="Filter">The optional strongly typed filter tree.</param>
/// <param name="Sorting">The ordered sort expressions.</param>
/// <param name="Paging">The optional paging request.</param>
public sealed record StateQueryDefinition(
    StateFilterNode? Filter,
    IReadOnlyList<Sorting> Sorting,
    Paging? Paging);

/// <summary>Base type for provider-neutral state filter nodes.</summary>
public abstract record StateFilterNode;

/// <summary>Represents one property comparison in a state query.</summary>
/// <param name="Operator">The provider-neutral comparison operator.</param>
/// <param name="Property">The strongly typed document property path.</param>
/// <param name="Value">The comparison value.</param>
public sealed record StateComparisonFilterNode(
    StateComparisonOperator Operator,
    LambdaExpression Property,
    object? Value) : StateFilterNode;

/// <summary>Represents a logical group of state query filters.</summary>
/// <param name="Operator">The logical group operator.</param>
/// <param name="Children">The grouped filter nodes.</param>
public sealed record StateGroupFilterNode(
    StateGroupOperator Operator,
    IReadOnlyList<StateFilterNode> Children) : StateFilterNode;

/// <summary>
/// Defines provider-neutral comparison operations supported by state queries.
/// </summary>
public enum StateComparisonOperator
{
    /// <summary>Tests values for equality.</summary>
    Equal,

    /// <summary>Tests whether a value belongs to a supplied set.</summary>
    In,

    /// <summary>Tests whether a value is greater than the supplied value.</summary>
    GreaterThan,

    /// <summary>Tests whether a value is greater than or equal to the supplied value.</summary>
    GreaterThanOrEqual,

    /// <summary>Tests whether a value is less than the supplied value.</summary>
    LessThan,

    /// <summary>Tests whether a value is less than or equal to the supplied value.</summary>
    LessThanOrEqual
}

/// <summary>
/// Defines provider-neutral logical operations used to combine state-query predicates.
/// </summary>
public enum StateGroupOperator
{
    /// <summary>Requires every child predicate to match.</summary>
    And,

    /// <summary>Requires at least one child predicate to match.</summary>
    Or
}
