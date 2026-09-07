using Nexus.Products.Chat.Domain.Branch;
using Nexus.Products.Chat.Domain.Conversation;

namespace Nexus.Products.Chat.Application.Branch.Queries.GetBranch;

public sealed record GetBranchResult(
    BranchId BranchId,
    ConversationId ConversationId,
    string Name,
    string Description,
    BranchStatus Status,
    DateTimeOffset CreatedAt,
    BranchId? ParentBranchId);