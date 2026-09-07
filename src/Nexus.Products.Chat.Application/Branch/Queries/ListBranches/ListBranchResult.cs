using Nexus.Products.Chat.Domain.Branch;

namespace Nexus.Products.Chat.Application.Branch.Queries.ListBranches;

public sealed record ListBranchResult(
    BranchId BranchId,
    string Name,
    string Description,
    BranchStatus Status,
    DateTimeOffset CreatedAt,
    BranchId? ParentBranchId);