namespace MoLibrary.Tool.Algorithm.Tree
{
    /// <summary>
    /// A Tree, which Node type is T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class TreeBase<T>
    {
        /// <summary>
        /// A Tree's root
        /// </summary>
        public T Root { get; set; }
    }
}