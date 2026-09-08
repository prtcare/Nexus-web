using Nexus.Products.Chat.Application.Branch.Commands;
using Nexus.Products.Chat.Application.Branch.Commands.UpdateBranch;
using Nexus.Products.Chat.Application.Branch.Queries.GetBranch;
using Nexus.Products.Chat.Application.Branch.Queries.ListBranches;
using Nexus.Products.Chat.Domain.Branch;
using Nexus.Products.Chat.Domain.Conversation;

namespace Nexus.Products.Chat.Api.Endpoints.Branches;

public static class BranchEndpoint
{
    public static IEndpointRouteBuilder MapBranchEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/v1/branches",
            async (
                CreateBranchRequest request,
                CreateBranchHandler handler,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.BadRequest(
                        new
                        {
                            error = "Name is required."
                        });
                }

                var result =
                    await handler.HandleAsync(
                        new CreateBranchCommand(
                            new ConversationId(
                                request.ConversationId),
                            request.Name,
                            request.Description,
                            request.ParentBranchId is null
                                ? null
                                : new BranchId(
                                    request.ParentBranchId.Value)),
                        cancellationToken);

                return Results.Ok(
                    new CreateBranchResponse(
                        result.BranchId.Value,
                        result.Name,
                        result.ParentBranchId?.Value));
            });

        app.MapGet(
            "/api/v1/branches/{id:guid}",
            async (
                Guid id,
                GetBranchHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result =
                    await handler.HandleAsync(
                        new GetBranchQuery(
                            new BranchId(id)),
                        cancellationToken);

                if (result is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(
                    new GetBranchResponse(
                        result.BranchId.Value,
                        result.ConversationId.Value,
                        result.Name,
                        result.Description,
                        (int)result.Status,
                        result.CreatedAt,
                        result.ParentBranchId?.Value));
            });

        app.MapGet(
            "/api/v1/conversations/{conversationId:guid}/branches",
            async (
                Guid conversationId,
                ListBranchesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result =
                    await handler.HandleAsync(
                        new ListBranchesQuery(
                            new ConversationId(
                                conversationId)),
                        cancellationToken);

                return Results.Ok(
                    result.Select(branch =>
                        new ListBranchResponse(
                            branch.BranchId.Value,
                            branch.Name,
                            branch.Description,
                            (int)branch.Status,
                            branch.CreatedAt,
                            branch.ParentBranchId?.Value)));
            });

        app.MapPut(
            "/api/v1/branches/{id:guid}",
            async (
                Guid id,
                UpdateBranchRequest request,
                UpdateBranchHandler handler,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.BadRequest(
                        new
                        {
                            error = "Name is required."
                        });
                }

                if (!Enum.IsDefined(
                        typeof(BranchStatus),
                        request.Status))
                {
                    return Results.BadRequest(
                        new
                        {
                            error =
                                $"Unsupported BranchStatus: {request.Status}."
                        });
                }

                var result =
                    await handler.HandleAsync(
                        new UpdateBranchCommand(
                            new BranchId(id),
                            request.Name,
                            request.Description,
                            (BranchStatus)request.Status,
                            request.ParentBranchId is null
                                ? null
                                : new BranchId(
                                    request.ParentBranchId.Value)),
                        cancellationToken);

                if (result is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(
                    new
                    {
                        branchId =
                            result.BranchId.Value,

                        result.Name,

                        result.Description,

                        status =
                            (int)result.Status,

                        parentBranchId =
                            result.ParentBranchId?.Value
                    });
            });

        return app;
    }
}