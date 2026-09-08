namespace Nexus.Products.Chat.Api.Endpoints.Branches;

public sealed record ListBranchResponse(
    Guid BranchId,
    string Name,
    string Description,
    int Status,
    DateTimeOffset CreatedAt,
    Guid? ParentBranchId = null);