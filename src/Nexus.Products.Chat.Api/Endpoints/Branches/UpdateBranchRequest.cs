namespace Nexus.Products.Chat.Api.Endpoints.Branches;

public sealed record UpdateBranchRequest(
    string Name,
    string Description,
    int Status,
    // SP1-D06: optional reparent target; omitted/null leaves the current parent unchanged.
    Guid? ParentBranchId = null);