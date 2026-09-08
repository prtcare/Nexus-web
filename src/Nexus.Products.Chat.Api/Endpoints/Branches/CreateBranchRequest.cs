namespace Nexus.Products.Chat.Api.Endpoints.Branches;

public sealed record CreateBranchRequest(
    Guid ConversationId,
    string Name,
    string Description,
    Guid? ParentBranchId = null);