using Nexus.Products.Chat.Domain.Branch;
using Nexus.Products.Chat.Domain.Conversation;

namespace Nexus.Products.Chat.Application.Branch.Commands;

public sealed record CreateBranchCommand(
    ConversationId ConversationId,
    string Name,
    string Description,
    // SP1-D06: optional parent branch within the same Conversation; null = root branch.
    BranchId? ParentBranchId = null);