using System.Diagnostics;

namespace Monica.Tool.Algorithms.Trees;

/// <summary>
/// A binary tree node with left and right children.
/// Separate from <see cref="TreeNode{TData}"/> due to different semantics.
/// </summary>
[DebuggerDisplay("{GetDebugDisplay()}")]
public class BinaryTreeNode<TData>
{
    private BinaryTreeNode<TData>? _left;
    private BinaryTreeNode<TData>? _right;

    /// <summary>Creates a new binary tree node with the specified data.</summary>
    public BinaryTreeNode(TData data) { Data = data; }

    /// <summary>The payload stored in this node.</summary>
    public TData Data { get; set; }

    /// <summary>The parent node. Null for root nodes.</summary>
    public BinaryTreeNode<TData>? Parent { get; private set; }

    /// <summary>The left child node.</summary>
    public BinaryTreeNode<TData>? Left
    {
        get => _left;
        set
        {
            if (_left is not null) _left.Parent = null;
            _left = value;
            if (_left is not null) _left.Parent = this;
        }
    }

    /// <summary>The right child node.</summary>
    public BinaryTreeNode<TData>? Right
    {
        get => _right;
        set
        {
            if (_right is not null) _right.Parent = null;
            _right = value;
            if (_right is not null) _right.Parent = this;
        }
    }

    /// <summary>Whether this node is a root (has no parent).</summary>
    public bool IsRoot => Parent is null;

    /// <summary>Whether this node is a leaf (has no children).</summary>
    public bool IsLeaf => _left is null && _right is null;

    /// <summary>The depth of this node (distance from root).</summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            var current = Parent;
            while (current is not null) { depth++; current = current.Parent; }
            return depth;
        }
    }

    /// <summary>Height of this subtree. A leaf node has height 0.</summary>
    public int Height
    {
        get
        {
            if (IsLeaf) return 0;
            var leftHeight = _left?.Height ?? -1;
            var rightHeight = _right?.Height ?? -1;
            return Math.Max(leftHeight, rightHeight) + 1;
        }
    }

    /// <summary>Total number of nodes in this subtree.</summary>
    public int Count => 1 + (_left?.Count ?? 0) + (_right?.Count ?? 0);

    /// <summary>The root node of the tree this node belongs to.</summary>
    public BinaryTreeNode<TData> Root
    {
        get
        {
            var current = this;
            while (current.Parent is not null) current = current.Parent;
            return current;
        }
    }

    private string GetDebugDisplay() =>
        $"Data={Data}, Left={(_left is not null ? "yes" : "no")}, Right={(_right is not null ? "yes" : "no")}";
}
