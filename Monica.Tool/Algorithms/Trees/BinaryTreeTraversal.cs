namespace Monica.Tool.Algorithm.Tree;

/// <summary>
/// Extension methods for traversing binary tree structures.
/// </summary>
public static class BinaryTreeTraversal
{
    /// <summary>Traverses in-order: left, node, right.</summary>
    public static IEnumerable<BinaryTreeNode<TData>> TraverseInOrder<TData>(
        this BinaryTreeNode<TData> node)
    {
        var stack = new Stack<BinaryTreeNode<TData>>();
        var current = node;
        while (current is not null || stack.Count > 0)
        {
            while (current is not null)
            {
                stack.Push(current);
                current = current.Left;
            }
            current = stack.Pop();
            yield return current;
            current = current.Right;
        }
    }

    /// <summary>Traverses pre-order: node, left, right.</summary>
    public static IEnumerable<BinaryTreeNode<TData>> TraversePreOrder<TData>(
        this BinaryTreeNode<TData> node)
    {
        var stack = new Stack<BinaryTreeNode<TData>>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            if (current.Right is not null) stack.Push(current.Right);
            if (current.Left is not null) stack.Push(current.Left);
        }
    }

    /// <summary>Traverses post-order: left, right, node.</summary>
    public static IEnumerable<BinaryTreeNode<TData>> TraversePostOrder<TData>(
        this BinaryTreeNode<TData> node)
    {
        var stack = new Stack<(BinaryTreeNode<TData> Node, bool Visited)>();
        stack.Push((node, false));
        while (stack.Count > 0)
        {
            var (current, visited) = stack.Pop();
            if (visited) { yield return current; continue; }
            stack.Push((current, true));
            if (current.Right is not null) stack.Push((current.Right, false));
            if (current.Left is not null) stack.Push((current.Left, false));
        }
    }

    /// <summary>Traverses level-order (breadth-first).</summary>
    public static IEnumerable<BinaryTreeNode<TData>> TraverseLevelOrder<TData>(
        this BinaryTreeNode<TData> node)
    {
        var queue = new Queue<BinaryTreeNode<TData>>();
        queue.Enqueue(node);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            if (current.Left is not null) queue.Enqueue(current.Left);
            if (current.Right is not null) queue.Enqueue(current.Right);
        }
    }
}
