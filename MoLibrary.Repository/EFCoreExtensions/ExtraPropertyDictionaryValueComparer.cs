using Microsoft.EntityFrameworkCore.ChangeTracking;
using MoLibrary.Repository.EntityInterfaces;

namespace MoLibrary.Repository.EFCoreExtensions;

public class ExtraPropertyDictionaryValueComparer : ValueComparer<ExtraPropertyDictionary>
{
    public ExtraPropertyDictionaryValueComparer()
        : base(
            (a, b) => Compare(a, b),
            d => d.Aggregate(0, (k, v) => HashCode.Combine(k, v.GetHashCode())),
            d => new ExtraPropertyDictionary(d))
    {
    }

    private static bool Compare(ExtraPropertyDictionary? a, ExtraPropertyDictionary? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null)
        {
            return b is null;
        }

        if (b is null)
        {
            return false;
        }

        return a!.SequenceEqual(b!);
    }
}
