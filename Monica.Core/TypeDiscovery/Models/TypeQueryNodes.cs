namespace Monica.Core.TypeDiscovery.Models;

internal sealed class ConstantTypeQuery(bool value) : TypeQuery
{
    internal bool Value { get; } = value;

    internal override TypeFactRequirements Requirements => TypeFactRequirements.None;

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches) => Value;

    public override bool Equals(TypeQuery? other) => other is ConstantTypeQuery constant && constant.Value == Value;

    public override int GetHashCode() => HashCode.Combine(typeof(ConstantTypeQuery), Value);

    public override string ToString() => Value ? "All" : "None";
}

internal sealed class ClassificationTypeQuery(TypeClassification classification) : TypeQuery
{
    private TypeClassification Classification => classification;

    internal override TypeFactRequirements Requirements => TypeFactRequirements.Classification;

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        return classification switch
        {
            TypeClassification.ConcreteClass => shape.IsConcreteClass,
            TypeClassification.ClosedClass => shape.IsConcreteClass && shape.IsClosedType,
            _ => throw new InvalidOperationException($"Unsupported type classification '{classification}'.")
        };
    }

    public override bool Equals(TypeQuery? other)
    {
        return other is ClassificationTypeQuery query && query.Classification == classification;
    }

    public override int GetHashCode() => HashCode.Combine(typeof(ClassificationTypeQuery), classification);

    public override string ToString() => classification.ToString();
}

internal sealed class RelatedTypeQuery(RelatedTypeRelation relation, Type targetType) : TypeQuery
{
    private RelatedTypeRelation Relation => relation;

    private Type TargetType => targetType;

    internal override TypeFactRequirements Requirements => relation switch
    {
        RelatedTypeRelation.AssignableTo => TypeFactRequirements.Assignability,
        RelatedTypeRelation.SubclassOf => TypeFactRequirements.BaseTypes,
        _ => TypeFactRequirements.None
    };

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        return relation switch
        {
            RelatedTypeRelation.AssignableTo => shape.IsAssignableTo(targetType),
            RelatedTypeRelation.SubclassOf => shape.IsSubclassOf(targetType),
            _ => throw new InvalidOperationException($"Unsupported type relation '{relation}'.")
        };
    }

    public override bool Equals(TypeQuery? other)
    {
        return other is RelatedTypeQuery query && query.Relation == relation && query.TargetType == targetType;
    }

    public override int GetHashCode() => HashCode.Combine(typeof(RelatedTypeQuery), relation, targetType);

    public override string ToString() => $"{relation}({targetType.FullName ?? targetType.Name})";
}

internal sealed class AttributeTypeQuery(Type attributeType, bool inherit) : TypeQuery
{
    private Type AttributeType => attributeType;

    private bool Inherit => inherit;

    internal override TypeFactRequirements Requirements =>
        TypeFactRequirements.CustomAttributes |
        (inherit ? TypeFactRequirements.BaseTypes : TypeFactRequirements.None);

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        return shape.HasAttribute(attributeType, inherit);
    }

    public override bool Equals(TypeQuery? other)
    {
        return other is AttributeTypeQuery query &&
               query.AttributeType == attributeType &&
               query.Inherit == inherit;
    }

    public override int GetHashCode() => HashCode.Combine(typeof(AttributeTypeQuery), attributeType, inherit);

    public override string ToString()
    {
        return $"HasAttribute({attributeType.FullName ?? attributeType.Name}, inherit: {inherit})";
    }
}

internal sealed class OpenGenericInterfaceTypeQuery(Type openGenericInterface) : TypeQuery
{
    private Type OpenGenericInterface => openGenericInterface;

    internal override TypeFactRequirements Requirements => TypeFactRequirements.OpenGenericInterfaces;

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        var matches = shape.GetOpenGenericInterfaceMatches(openGenericInterface);
        if (matches.Count == 0)
        {
            return false;
        }

        genericMatches?.AddRange(matches);
        return true;
    }

    public override bool Equals(TypeQuery? other)
    {
        return other is OpenGenericInterfaceTypeQuery query &&
               query.OpenGenericInterface == openGenericInterface;
    }

    public override int GetHashCode() => HashCode.Combine(typeof(OpenGenericInterfaceTypeQuery), openGenericInterface);

    public override string ToString()
    {
        return $"ImplementsOpenGeneric({openGenericInterface.FullName ?? openGenericInterface.Name})";
    }
}

internal sealed class NotTypeQuery(TypeQuery operand) : TypeQuery
{
    internal TypeQuery Operand { get; } = operand;

    internal override TypeFactRequirements Requirements => Operand.Requirements;

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        var initialMatchCount = genericMatches?.Count ?? 0;
        var matched = Operand.Evaluate(shape, genericMatches);
        TypeQueryEvaluation.RemoveMatchesAfter(genericMatches, initialMatchCount);
        return !matched;
    }

    public override bool Equals(TypeQuery? other) => other is NotTypeQuery not && Operand.Equals(not.Operand);

    public override int GetHashCode() => HashCode.Combine(typeof(NotTypeQuery), Operand);

    public override string ToString() => $"Not({Operand})";
}

internal sealed class CompositeTypeQuery(CompositeOperator @operator, TypeQuery[] operands) : TypeQuery
{
    internal CompositeOperator Operator { get; } = @operator;

    internal IReadOnlyList<TypeQuery> Operands { get; } = Array.AsReadOnly(operands);

    internal override TypeFactRequirements Requirements
    {
        get
        {
            var requirements = TypeFactRequirements.None;
            foreach (var operand in Operands)
            {
                requirements |= operand.Requirements;
            }

            return requirements;
        }
    }

    internal override bool Evaluate(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        return Operator == CompositeOperator.All
            ? EvaluateAll(shape, genericMatches)
            : EvaluateAny(shape, genericMatches);
    }

    public override bool Equals(TypeQuery? other)
    {
        return other is CompositeTypeQuery composite &&
               composite.Operator == Operator &&
               Operands.SequenceEqual(composite.Operands);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(typeof(CompositeTypeQuery));
        hash.Add(Operator);
        foreach (var operand in Operands)
        {
            hash.Add(operand);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => $"{Operator}Of({string.Join(", ", Operands)})";

    private bool EvaluateAll(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        var initialMatchCount = genericMatches?.Count ?? 0;
        foreach (var operand in Operands)
        {
            if (operand.Evaluate(shape, genericMatches))
            {
                continue;
            }

            TypeQueryEvaluation.RemoveMatchesAfter(genericMatches, initialMatchCount);
            return false;
        }

        return true;
    }

    private bool EvaluateAny(BusinessTypeShape shape, List<OpenGenericInterfaceMatch>? genericMatches)
    {
        var initialMatchCount = genericMatches?.Count ?? 0;
        var matched = false;
        foreach (var operand in Operands)
        {
            var branchMatchCount = genericMatches?.Count ?? 0;
            if (operand.Evaluate(shape, genericMatches))
            {
                matched = true;
            }
            else
            {
                TypeQueryEvaluation.RemoveMatchesAfter(genericMatches, branchMatchCount);
            }
        }

        if (!matched)
        {
            TypeQueryEvaluation.RemoveMatchesAfter(genericMatches, initialMatchCount);
        }

        return matched;
    }
}

internal static class TypeQueryEvaluation
{
    internal static void RemoveMatchesAfter(List<OpenGenericInterfaceMatch>? matches, int count)
    {
        if (matches is not null && matches.Count > count)
        {
            matches.RemoveRange(count, matches.Count - count);
        }
    }
}

internal enum TypeClassification
{
    ConcreteClass,
    ClosedClass
}

internal enum RelatedTypeRelation
{
    AssignableTo,
    SubclassOf
}

internal enum CompositeOperator
{
    All,
    Any
}
