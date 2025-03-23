namespace MoLibrary.Tool.Algorithm;

public class Normalizer
{
    /// <summary>
    /// Normalize to 0 - 1.
    /// </summary>
    /// <param name="num"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public double CommonNormalize(double num, double min, double max) => (num - min) / (max - min);
}