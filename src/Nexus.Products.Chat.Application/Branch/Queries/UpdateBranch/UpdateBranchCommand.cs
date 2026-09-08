using Nexus.Products.Chat.Domain.Branch;

namespace Nexus.Products.Chat.Application.Branch.Commands.UpdateBranch;

public sealed record UpdateBranchCommand(
    BranchId BranchId,
    string Name,
    string Description,
    BranchStatus Status,
    // SP1-D06: optional reparent target. null means "leave the current parent unchanged"
    // so pre-existing clients that PUT without a parent keep working exactly as before.
    BranchId? ParentBranchId = null);