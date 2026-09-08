using Nexus.Products.Chat.Domain.Branch;

namespace Nexus.Products.Chat.Application.Branch.Commands;

public sealed class CreateBranchHandler
{
    private readonly IBranchRepository _repository;
    private readonly TimeProvider _timeProvider;

    public CreateBranchHandler(
        IBranchRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<CreateBranchResult> HandleAsync(
        CreateBranchCommand command,
        CancellationToken cancellationToken = default)
    {
        // SP1-D06 (Subchat recursion): when an optional parent is supplied it must exist
        // and live in the same Conversation - a branch is only ever a thread of its own
        // Conversation, never of another Conversation's branch tree.
        if (command.ParentBranchId is not null)
        {
            var parent = await _repository.GetAsync(
                command.ParentBranchId.Value,
                cancellationToken);

            if (parent is null)
            {
                throw new InvalidOperationException(
                    "Parent branch not found; a branch can only be created under an "
                    + "existing branch of the same Conversation.");
            }

            if (parent.ConversationId != command.ConversationId)
            {
                throw new InvalidOperationException(
                    "Parent branch belongs to a different Conversation; a sub-branch "
                    + "must stay within its parent branch's Conversation.");
            }
        }

        var branch =
            new Nexus.Products.Chat.Domain.Branch.Branch(
                BranchId.New(),
                command.ConversationId,
                command.Name,
                command.Description,
                BranchStatus.Active,
                _timeProvider.GetUtcNow(),
                command.ParentBranchId);

        await _repository.AddAsync(
            branch,
            cancellationToken);

        return new CreateBranchResult(
            branch.Id,
            branch.Name,
            branch.ParentBranchId);
    }
}
