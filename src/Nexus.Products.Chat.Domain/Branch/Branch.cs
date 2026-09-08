using Nexus.Products.Chat.Domain.Common;
using Nexus.Products.Chat.Domain.Conversation;

namespace Nexus.Products.Chat.Domain.Branch;

public sealed class Branch : Entity<BranchId>
{
    public Branch(
        BranchId id,
        ConversationId conversationId,
        string name,
        string description,
        BranchStatus status,
        DateTimeOffset createdAt,
        BranchId? parentBranchId = null)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (parentBranchId == id)
        {
            throw new ArgumentException(
                "A branch cannot be its own parent.",
                nameof(parentBranchId));
        }

        ConversationId = conversationId;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Status = status;
        CreatedAt = createdAt;
        ParentBranchId = parentBranchId;
    }

    public ConversationId ConversationId { get; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public BranchStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Optional parent branch within the same conversation. A null value means this
    /// branch is a root branch (the default). SP1-D06 (Subchat recursion): a branch
    /// may branch from a parent branch, forming a thread/sub-chat tree under a
    /// Conversation while Conversation remains the canonical chat aggregate.
    /// </summary>
    public BranchId? ParentBranchId { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
    }

    public void UpdateDescription(string description)
    {
        Description = description?.Trim() ?? string.Empty;
    }

    public void ChangeStatus(BranchStatus status)
    {
        Status = status;
    }

    /// <summary>
    /// Moves this branch under <paramref name="parentBranchId"/> (or to the root when
    /// null). SP1-D06 rules:
    /// <list type="bullet">
    /// <item>self-parent is rejected - a branch cannot be its own parent;</item>
    /// <item>cycle is rejected - the branch cannot become a descendant of itself, i.e.
    /// <see cref="Id"/> must not appear among the proposed parent's existing ancestors
    /// (<paramref name="newParentAncestorBranchIds"/>). The caller is responsible for
    /// supplying that ancestor chain, collected from persistence.</item>
    /// </list>
    /// </summary>
    public void ChangeParent(
        BranchId? parentBranchId,
        IReadOnlyCollection<BranchId> newParentAncestorBranchIds)
    {
        if (parentBranchId == Id)
        {
            throw new InvalidOperationException(
                "A branch cannot be its own parent.");
        }

        if (parentBranchId is not null
            && newParentAncestorBranchIds.Contains(Id))
        {
            throw new InvalidOperationException(
                "A branch cannot be reparented to one of its own descendants; "
                + "this would create a cycle in the branch hierarchy.");
        }

        ParentBranchId = parentBranchId;
    }

    public void Archive()
    {
        Status = BranchStatus.Archived;
    }

    public void Merge()
    {
        Status = BranchStatus.Merged;
    }
}