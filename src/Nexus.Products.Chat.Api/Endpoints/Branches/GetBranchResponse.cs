namespace Nexus.Products.Chat.Api.Endpoints.Branches;

public sealed record GetBranchResponse(
    Guid BranchId,
    Guid ConversationId,
    string Name,
    string Description,
    int Status,
    DateTimeOffset CreatedAt,
    Guid? ParentBranchId = null);