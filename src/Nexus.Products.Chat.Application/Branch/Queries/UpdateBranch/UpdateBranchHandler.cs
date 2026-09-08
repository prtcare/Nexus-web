using Nexus.Products.Chat.Domain.Branch;

namespace Nexus.Products.Chat.Application.Branch.Commands.UpdateBranch;

public sealed class UpdateBranchHandler
{
    private readonly IBranchRepository _repository;

    public UpdateBranchHandler(
        IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<UpdateBranchResult?> HandleAsync(
        UpdateBranchCommand command,
        CancellationToken cancellationToken = default)
    {
        var branch = await _repository.GetAsync(
            command.BranchId,
            cancellationToken);

        if (branch is null)
        {
            return null;
        }

        // SP1-D06 (Subchat recursion): an explicit ParentBranchId reparents this branch.
        // null leaves the current parent untouched, so pre-existing callers are unaffected.
        if (command.ParentBranchId is not null)
        {
            var parent = await _repository.GetAsync(
                command.ParentBranchId.Value,
                cancellationToken);

            if (parent is null)
            {
                throw new InvalidOperationException(
                    "Parent branch not found; a branch can only be reparented under an "
                    + "existing branch of the same Conversation.");
            }

            if (parent.ConversationId != branch.ConversationId)
            {
                throw new InvalidOperationException(
                    "Parent branch belongs to a different Conversation; a branch must "
                    + "stay within its parent branch's Conversation.");
            }

            // The new parent's existing ancestors are walked from persistence so the
            // domain can reject a reparent that would make this branch a descendant of
            // itself (direct or transitive cycle).
            var newParentAncestors =
                await CollectAncestorBranchIdsAsync(
                    parent,
                    cancellationToken);

            branch.ChangeParent(
                command.ParentBranchId,
                newParentAncestors);
        }

        branch.Rename(command.Name);
        branch.UpdateDescription(command.Description);
        branch.ChangeStatus(command.Status);

        await _repository.UpdateAsync(
            branch,
            cancellationToken);

        return new UpdateBranchResult(
            branch.Id,
            branch.Name,
            branch.Description,
            branch.Status,
            branch.ParentBranchId);
    }

    private async Task<IReadOnlyCollection<BranchId>> CollectAncestorBranchIdsAsync(
        Nexus.Products.Chat.Domain.Branch.Branch branch,
        CancellationToken cancellationToken)
    {
        var ancestors = new List<BranchId>();
        var seen = new HashSet<BranchId> { branch.Id };

        BranchId? current = branch.ParentBranchId;
        while (current is not null)
        {
            if (!seen.Add(current.Value))
            {
                throw new InvalidOperationException(
                    "Branch hierarchy is corrupt; a cycle was detected in the persisted "
                    + "ParentBranchId chain.");
            }

            ancestors.Add(current.Value);

            var ancestor = await _repository.GetAsync(
                current.Value,
                cancellationToken);

            if (ancestor is null)
            {
                throw new InvalidOperationException(
                    "Branch hierarchy is corrupt; a persisted ParentBranchId points at a "
                    + "branch that no longer exists.");
            }

            current = ancestor.ParentBranchId;
        }

        return ancestors;
    }
}
