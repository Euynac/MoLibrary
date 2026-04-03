using System.Collections;
using System.Diagnostics;

namespace Monica.Tool.Algorithms.Trees;

/// <summary>
/// A generic tree node that serves as both node and subtree root.
/// Implements DFS pre-order traversal via <see cref="IEnumerable{T}"/>.
/// </summary>
/// <typeparam name="TData">The type of data stored in each node.</typeparam>
[DebuggerDisplay("{GetDebugDisplay()}")]
public class TreeNode<TData> : IEnumerable<TreeNode<TData>>
{
    private readonly List<TreeNode<TData>> _children = [];

    /// <summary>
    /// Creates a new tree node with the specified data.
    /// </summary>
    public TreeNode(TData data)
    {
        Data = data;
    }

    /// <summary>
    /// The payload stored in this node.
    /// </summary>
    public TData Data { get; set; }

    /// <summary>
    /// The parent node. Null for root nodes.
    /// </summary>
    public TreeNode<TData>? Parent { get; private set; }

    /// <summary>
    /// The children of this node.
    /// </summary>
    public IReadOnlyList<TreeNode<TData>> Children => _children;

    /// <summary>
    /// The depth of this node (distance from root). Root has depth 0.
    /// </summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            var current = Parent;
            while (current is not null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }
    }

    /// <summary>
    /// Whether this node is a root (has no parent).
    /// </summary>
    public bool IsRoot => Parent is null;

    /// <summary>
    /// Whether this node is a leaf (has no children).
    /// </summary>
    public bool IsLeaf => _children.Count == 0;

    /// <summary>
    /// Total number of nodes in this subtree (including this node).
    /// </summary>
    public int Count
    {
        get
        {
            var count = 1;
            foreach (var child in _children)
            {
                count += child.Count;
            }
            return count;
        }
    }

    /// <summary>
    /// Height of this subtree. A leaf node has height 0.
    /// </summary>
    public int Height
    {
        get
        {
            if (_children.Count == 0) return 0;
            var maxChildHeight = 0;
            foreach (var child in _children)
            {
                var childHeight = child.Height;
                if (childHeight > maxChildHeight)
                    maxChildHeight = childHeight;
            }
            return maxChildHeight + 1;
        }
    }

    /// <summary>
    /// The root node of the tree this node belongs to.
    /// </summary>
    public TreeNode<TData> Root
    {
        get
        {
            var current = this;
            while (current.Parent is not null)
            {
                current = current.Parent;
            }
            return current;
        }
    }

    /// <summary>
    /// Adds an existing node as a child. Detaches it from its current parent if any.
    /// </summary>
    /// <returns>This node (for chaining).</returns>
    public TreeNode<TData> AddChild(TreeNode<TData> child)
    {
        child.Detach();
        child.Parent = this;
        _children.Add(child);
        return this;
    }

    /// <summary>
    /// Creates a new child node with the specified data and adds it.
    /// </summary>
    /// <returns>The newly created child node.</returns>
    public TreeNode<TData> AddChild(TData data)
    {
        var child = new TreeNode<TData>(data) { Parent = this };
        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Adds multiple existing nodes as children.
    /// </summary>
    /// <returns>This node (for chaining).</returns>
    public TreeNode<TData> AddChildren(IEnumerable<TreeNode<TData>> children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
        return this;
    }

    /// <summary>
    /// Creates and adds multiple child nodes from data values.
    /// </summary>
    /// <returns>This node (for chaining).</returns>
    public TreeNode<TData> AddChildren(params TData[] dataItems)
    {
        foreach (var data in dataItems)
        {
            AddChild(data);
        }
        return this;
    }

    /// <summary>
    /// Removes a child node from this node's children.
    /// </summary>
    /// <returns>True if the child was found and removed.</returns>
    public bool RemoveChild(TreeNode<TData> child)
    {
        if (!_children.Remove(child)) return false;
        child.Parent = null;
        return true;
    }

    /// <summary>
    /// Detaches this node from its parent.
    /// </summary>
    /// <returns>This node.</returns>
    public TreeNode<TData> Detach()
    {
        Parent?._children.Remove(this);
        Parent = null;
        return this;
    }

    /// <summary>
    /// Sorts the immediate children using the specified comparison.
    /// </summary>
    /// <returns>This node (for chaining).</returns>
    public TreeNode<TData> SortChildren(Comparison<TreeNode<TData>> comparison)
    {
        _children.Sort(comparison);
        return this;
    }

    /// <summary>
    /// Sorts the immediate children by a key.
    /// </summary>
    /// <returns>This node (for chaining).</returns>
    public TreeNode<TData> SortChildren<TKey>(Func<TreeNode<TData>, TKey> keySelector, bool descending = false)
    {
        var comparer = Comparer<TKey>.Default;
        _children.Sort((a, b) =>
        {
            var result = comparer.Compare(keySelector(a), keySelector(b));
            return descending ? -result : result;
        });
        return this;
    }

    /// <summary>
    /// Recursively sorts children at all levels using the specified comparison.
    /// </summary>
    /// <returns>This node (for chaining).</returns>
    public TreeNode<TData> SortChildrenRecursive(Comparison<TreeNode<TData>> comparison)
    {
        _children.Sort(comparison);
        foreach (var child in _children)
        {
            child.SortChildrenRecursive(comparison);
        }
        return this;
    }

    /// <summary>
    /// Default enumerator: DFS pre-order traversal of this subtree.
    /// </summary>
    public IEnumerator<TreeNode<TData>> GetEnumerator()
    {
        var stack = new Stack<TreeNode<TData>>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current._children.Count - 1; i >= 0; i--)
            {
                stack.Push(current._children[i]);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string GetDebugDisplay()
    {
        return $"Data={Data}, Children={_children.Count}, Depth={Depth}";
    }
}
