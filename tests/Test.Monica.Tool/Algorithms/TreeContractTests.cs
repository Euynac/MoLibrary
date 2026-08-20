using Monica.Tool.Algorithms.Trees;

namespace Test.Monica.Tool.Algorithms;

public sealed class TreeContractTests
{
    [Fact]
    public void AddChild_WhenNodeHasAParent_ShouldReparentItAtomically()
    {
        var firstParent = new TreeNode<string>("first");
        var secondParent = new TreeNode<string>("second");
        var child = firstParent.AddChild("child");

        secondParent.AddChild(child);

        child.Parent.Should().BeSameAs(secondParent);
        firstParent.Children.Should().BeEmpty();
        secondParent.Children.Should().ContainSingle().Which.Should().BeSameAs(child);
    }

    [Fact]
    public void AddChild_WhenItWouldCreateCycle_ShouldRejectIt()
    {
        var root = new TreeNode<string>("root");
        var child = root.AddChild("child");

        var selfAct = () => root.AddChild(root);
        var ancestorAct = () => child.AddChild(root);

        selfAct.Should().Throw<InvalidOperationException>();
        ancestorAct.Should().Throw<InvalidOperationException>();
        root.Parent.Should().BeNull();
        child.Parent.Should().BeSameAs(root);
    }

    [Fact]
    public void Traverse_WhenTreeIsPopulated_ShouldRespectRequestedOrder()
    {
        var root = new TreeNode<int>(1);
        var left = root.AddChild(2);
        left.AddChildren(4, 5);
        root.AddChild(3);

        root.Traverse(TreeTraversalOrder.PreOrder).Select(node => node.Data)
            .Should().Equal(1, 2, 4, 5, 3);
        root.Traverse(TreeTraversalOrder.PostOrder).Select(node => node.Data)
            .Should().Equal(4, 5, 2, 3, 1);
        root.Traverse(TreeTraversalOrder.BreadthFirst).Select(node => node.Data)
            .Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void BuildFromFlat_WhenChildrenPrecedeParents_ShouldBuildOneTree()
    {
        var items = new[]
        {
            new FlatNode("3", "2"),
            new FlatNode("2", "1"),
            new FlatNode("1", null),
        };

        var roots = TreeBuilder.BuildFromFlat(items, item => item.Id, item => item.ParentId);

        roots.Should().ContainSingle();
        roots[0].Select(node => node.Data.Id).Should().Equal("1", "2", "3");
    }

    [Fact]
    public void BuildFromFlat_WhenKeysAreDuplicated_ShouldRejectAmbiguousTree()
    {
        var items = new[] { new FlatNode("1", null), new FlatNode("1", null) };

        var act = () => TreeBuilder.BuildFromFlat(items, item => item.Id, item => item.ParentId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildFromFlat_WhenParentKeysFormCycle_ShouldRejectIt()
    {
        var items = new[] { new FlatNode("1", "2"), new FlatNode("2", "1") };

        var act = () => TreeBuilder.BuildFromFlat(items, item => item.Id, item => item.ParentId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddChildren_WhenMovingLiveChildrenView_ShouldMaterializeBeforeMutation()
    {
        var source = new TreeNode<int>(1);
        source.AddChildren(2, 3);
        var target = new TreeNode<int>(4);

        target.AddChildren(source.Children);

        source.Children.Should().BeEmpty();
        target.Children.Select(node => node.Data).Should().Equal(2, 3);
    }

    [Fact]
    public void BinaryChild_WhenMovedBetweenParents_ShouldClearOldParentLink()
    {
        var first = new BinaryTreeNode<int>(1);
        var second = new BinaryTreeNode<int>(2);
        var child = new BinaryTreeNode<int>(3);
        first.Left = child;

        second.Right = child;

        first.Left.Should().BeNull();
        second.Right.Should().BeSameAs(child);
        child.Parent.Should().BeSameAs(second);
    }

    [Fact]
    public void BinaryChild_WhenItWouldCreateCycle_ShouldRejectIt()
    {
        var root = new BinaryTreeNode<int>(1);
        var child = new BinaryTreeNode<int>(2);
        root.Left = child;

        var act = () => child.Right = root;

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed record FlatNode(string Id, string? ParentId);
}
