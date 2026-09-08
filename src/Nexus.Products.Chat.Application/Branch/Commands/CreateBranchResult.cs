using Nexus.Products.Chat.Domain.Branch;

namespace Nexus.Products.Chat.Application.Branch.Commands;

public sealed record CreateBranchResult(
    BranchId BranchId,
    string Name,
    BranchId? ParentBranchId);