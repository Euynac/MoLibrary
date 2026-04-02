namespace Monica.Tool.Abstractions;

/// <summary>
/// Marks a type whose public properties can participate in signature generation.
/// </summary>
public interface ISignable;

/// <summary>
/// Marks a strongly typed signable model.
/// </summary>
/// <typeparam name="TSelf">The concrete model type.</typeparam>
public interface ISignable<TSelf> : ISignable
    where TSelf : class, ISignable<TSelf>;
