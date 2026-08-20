namespace Monica.Tool.Algorithms;

public class Normalizer
{
    /// <summary>
    /// Normalize to 0 - 1.
    /// </summary>
    /// <param name="num"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public double CommonNormalize(double num, double min, double max)
    {
        if (!double.IsFinite(num))
        {
            throw new ArgumentOutOfRangeException(nameof(num), "The value must be finite.");
        }

        if (!double.IsFinite(min))
        {
            throw new ArgumentOutOfRangeException(nameof(min), "The lower bound must be finite.");
        }

        if (!double.IsFinite(max) || max <= min)
        {
            throw new ArgumentException("The upper bound must be finite and greater than the lower bound.", nameof(max));
        }

        return (num - min) / (max - min);
    }
}
