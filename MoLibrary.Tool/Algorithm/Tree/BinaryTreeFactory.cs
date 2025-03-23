namespace MoLibrary.Tool.Algorithm.Tree
{
    /// <summary>
    /// BinaryTree Factory for getting specific Binary Tree
    /// </summary>
    public static class BinaryTreeFactory<T>
    {
        /// <summary>
        /// The underlying structure representation type.
        /// </summary>
        public enum StructureType
        {
            /// <summary>
            /// 
            /// </summary>
            ContiguousArray,
            /// <summary>
            /// 
            /// </summary>
            DynamicallyLinked
        }

        /// <summary>
        /// Get a binary tree constructed in specific way.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static BinaryTree<T> Construct(StructureType type)
        {
            return null;
        }
    }
}