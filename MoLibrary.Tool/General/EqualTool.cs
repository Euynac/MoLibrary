using System;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// Helper for override Equal and HashCode
    /// </summary>
    public static class EqualTool
    {
        /// <summary>
        /// Try to test whether given two probable default objects are equal.
        /// </summary>
        /// <param name="obj">Placeholder, it's useless for the compare result.</param>
        /// <param name="obj1">The first element wants to compare</param>
        /// <param name="obj2">The second element wants to compare</param>
        /// <param name="bothDefaultReturnTrue">Default setting is that if both objects are default(null is class while 0 is value type), it will return false means can't to compare and need use another fields.</param>
        /// <returns>Return false means you need to use another fields to compare.</returns>
        public static bool TryEqual(this object obj, object? obj1, object? obj2, bool bothDefaultReturnTrue = false)
        {
            if (obj1 == default)
            {
                return obj2 == default && bothDefaultReturnTrue;
            }

            return obj2 != default && obj1.Equals(obj2);
        }

        /// <summary>
        /// Compute joint hashcode with all given objects.
        /// </summary>
        /// <param name="obj">The obj also will be computed hashcode.</param>
        /// <param name="toJointComputedObjects">The objects which needed to be computed hashcode jointly.</param>
        /// <returns></returns>
        /// Use HashCode.Combine instead?https://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-overriding-gethashcode
        [Obsolete("Use HashCode.Combine instead?")]
        public static int GetHashCodeWith(this object? obj, params object[] toJointComputedObjects)
        {
            var hash = 13;
            hash = hash * 7 + (obj?.GetHashCode() ?? 0);
            foreach (var o in toJointComputedObjects)
            {
                hash = hash * 7 + (o?.GetHashCode() ?? 0);
            }

            return hash;
        }

    }
}