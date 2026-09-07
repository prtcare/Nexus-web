using Nexus.Products.Chat.Domain.Branch;
using Nexus.Products.Chat.Domain.Conversation;
using Xunit;
using BranchEntity = Nexus.Products.Chat.Domain.Branch.Branch;

namespace Nexus.Products.Chat.Tests.Branch;

/// <summary>
/// SP1-D06 (Subchat recursion): aggregate-level invariants for the optional
/// Branch.ParentBranchId link. These tests exercise the domain rules directly
/// (self-parent rejection and the ancestor-cycle guard); application-handler
/// enforcement is covered separately in <see cref="BranchHierarchyApplicationTests"/>.
/// </summary>
public sealed class BranchHierarchyTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);

    private static readonly ConversationId ConversationId = ConversationId.New();

    private static BranchEntity CreateBranch(
        string name,
        BranchId? parentBranchId = null)
        => new(
            BranchId.New(),
            ConversationId,
            name,
            string.Empty,
            BranchStatus.Active,
            FixedNow,
            parentBranchId);

    // --- root branch default -------------------------------------------------

    [Fact]
    public void Create_WithoutParent_ParentBranchIdIsNull_RootBranch()
    {
        var branch = CreateBranch("Root");

        Assert.Null(branch.ParentBranchId);
    }

    [Fact]
    public void Create_RootBranch_ExistingBehaviorIsPreserved()
    {
        var branch = CreateBranch("  Trimmed Root  ");

        Assert.Equal("Trimmed Root", branch.Name);
        Assert.Equal(BranchStatus.Active, branch.Status);
        Assert.Equal(string.Empty, branch.Description);
        Assert.Null(branch.ParentBranchId);

        branch.Rename("Renamed");
        branch.UpdateDescription("Updated description");
        branch.ChangeStatus(BranchStatus.Merged);

        Assert.Equal("Renamed", branch.Name);
        Assert.Equal("Updated description", branch.Description);
        Assert.Equal(BranchStatus.Merged, branch.Status);
        Assert.Null(branch.ParentBranchId);
    }

    // --- sub-branch creation -------------------------------------------------

    [Fact]
    public void Create_WithParent_ParentBranchIdIsSet()
    {
        var parent = CreateBranch("Parent");
        var child = CreateBranch("Child", parent.Id);

        Assert.Equal(parent.Id, child.ParentBranchId!.Value);
    }

    // --- self-parent rejection ----------------------------------------------

    [Fact]
    public void Create_WithSelfAsParent_Throws()
    {
        var id = BranchId.New();

        var exception = Assert.Throws<ArgumentException>(
            () => new BranchEntity(
                id,
                ConversationId,
                "Self",
                string.Empty,
                BranchStatus.Active,
                FixedNow,
                id));

        Assert.Contains("own parent", exception.Message);
    }

    [Fact]
    public void ChangeParent_ToSelf_Throws()
    {
        var branch = CreateBranch("Root");

        var exception = Assert.Throws<InvalidOperationException>(
            () => branch.ChangeParent(branch.Id, []));

        Assert.Contains("own parent", exception.Message);
    }

    // --- cycle rejection -----------------------------------------------------

    [Fact]
    public void ChangeParent_DirectCycleAParentsToB_Throws()
    {
        // A -> B (B is a sub-branch of A). Reparenting A under B would make A its own
        // ancestor (A -> B -> A); ancestors(B) = { A }.
        var a = CreateBranch("A");
        var b = CreateBranch("B", a.Id);

        var exception = Assert.Throws<InvalidOperationException>(
            () => a.ChangeParent(b.Id, [a.Id]));

        Assert.Contains("cycle", exception.Message);
        Assert.Null(a.ParentBranchId);
    }

    [Fact]
    public void ChangeParent_DeeperCycleAParentsToC_Throws()
    {
        // A -> B -> C. Reparenting A under C would create A -> C -> B -> A;
        // ancestors(C) = { B, A }.
        var a = CreateBranch("A");
        var b = CreateBranch("B", a.Id);
        var c = CreateBranch("C", b.Id);

        var exception = Assert.Throws<InvalidOperationException>(
            () => a.ChangeParent(c.Id, [b.Id, a.Id]));

        Assert.Contains("cycle", exception.Message);
        Assert.Null(a.ParentBranchId);
    }

    [Fact]
    public void ChangeParent_ToAncestorWithoutCycle_IsAllowed()
    {
        // B is a root sibling of A; moving B under A is legal because A is not a
        // descendant of B (ancestors(A) is empty).
        var a = CreateBranch("A");
        var b = CreateBranch("B");

        b.ChangeParent(a.Id, []);

        Assert.Equal(a.Id, b.ParentBranchId!.Value);
    }

    [Fact]
    public void ChangeParent_ToNull_MovesBranchBackToRoot()
    {
        var parent = CreateBranch("Parent");
        var child = CreateBranch("Child", parent.Id);

        child.ChangeParent(null, []);

        Assert.Null(child.ParentBranchId);
    }
}
