namespace MoLibrary.Tool.General;

/// <summary>
/// Mutable tuple class for convenient in some scenarios.
/// </summary>
/// <typeparam name="T1"></typeparam>
/// <typeparam name="T2"></typeparam>
public class MutableTuple<T1, T2> {
    public T1 Item1 { get; set; }
    public T2 Item2 { get; set; }
    /// <summary>
    /// Mutable tuple class for convenient in some scenarios.
    /// </summary>
    public MutableTuple(T1 item1, T2 item2) {
        Item1 = item1;
        Item2 = item2;
    }
}