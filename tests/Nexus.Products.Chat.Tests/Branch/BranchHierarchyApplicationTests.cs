using Nexus.Products.Chat.Application.Branch.Commands;
using Nexus.Products.Chat.Application.Branch.Commands.UpdateBranch;
using Nexus.Products.Chat.Application.Branch.Queries.GetBranch;
using Nexus.Products.Chat.Application.Branch.Queries.ListBranches;
using Nexus.Products.Chat.Domain.Branch;
using Nexus.Products.Chat.Domain.Conversation;
using Xunit;
using BranchEntity = Nexus.Products.Chat.Domain.Branch.Branch;

namespace Nexus.Products.Chat.Tests.Branch;

/// <summary>
/// SP1-D06 (Subchat recursion): application-handler enforcement of the optional
/// Branch.ParentBranchId link. The handlers are where the persisted ancestry chain is
/// actually walked, so these tests prove the authoritative self-parent / ancestor-cycle
/// guards (a pure domain call that trusted its caller could be bypassed).
/// </summary>
public sealed class BranchHierarchyApplicationTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);

    private static BranchEntity RootBranch(ConversationId conversationId, string name)
        => new(
            BranchId.New(),
            conversationId,
            name,
            string.Empty,
            BranchStatus.Active,
            FixedNow);

    private static BranchEntity SubBranch(
        ConversationId conversationId,
        string name,
        BranchId parentBranchId)
        => new(
            BranchId.New(),
            conversationId,
            name,
            string.Empty,
            BranchStatus.Active,
            FixedNow,
            parentBranchId);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }

    private sealed class InMemoryBranchRepository : IBranchRepository
    {
        private readonly Dictionary<BranchId, BranchEntity> _branches = new();

        public Task AddAsync(
            BranchEntity branch,
            CancellationToken cancellationToken = default)
        {
            _branches[branch.Id] = branch;
            return Task.CompletedTask;
        }

        public Task<BranchEntity?> GetAsync(
            BranchId id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                _branches.TryGetValue(id, out var branch)
                    ? branch
                    : null);

        public Task UpdateAsync(
            BranchEntity branch,
            CancellationToken cancellationToken = default)
        {
            _branches[branch.Id] = branch;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BranchEntity>> ListByConversationAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BranchEntity>>(
                _branches.Values
                    .Where(branch => branch.ConversationId == conversationId)
                    .OrderBy(branch => branch.CreatedAt)
                    .ToList());
    }

    // --- create: root default & sub-branch support --------------------------

    [Fact]
    public async Task Create_RootBranch_StoresRootWithoutParent()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var handler = new CreateBranchHandler(repository, new FixedTimeProvider());

        var result = await handler.HandleAsync(
            new CreateBranchCommand(conversationId, "Root", string.Empty));

        Assert.Null(result.ParentBranchId);

        var stored = await repository.GetAsync(result.BranchId);
        Assert.NotNull(stored);
        Assert.Null(stored!.ParentBranchId);
    }

    [Fact]
    public async Task Create_WithExistingParent_StoresSubBranch()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var parent = RootBranch(conversationId, "Parent");
        await repository.AddAsync(parent);
        var handler = new CreateBranchHandler(repository, new FixedTimeProvider());

        var result = await handler.HandleAsync(
            new CreateBranchCommand(conversationId, "Child", string.Empty, parent.Id));

        Assert.Equal(parent.Id, result.ParentBranchId!.Value);

        var stored = await repository.GetAsync(result.BranchId);
        Assert.NotNull(stored);
        Assert.Equal(parent.Id, stored!.ParentBranchId!.Value);
    }

    [Fact]
    public async Task Create_WithMissingParent_Throws()
    {
        var conversationId = ConversationId.New();
        var handler = new CreateBranchHandler(
            new InMemoryBranchRepository(),
            new FixedTimeProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new CreateBranchCommand(
                    conversationId,
                    "Child",
                    string.Empty,
                    BranchId.New())));

        Assert.Contains("Parent branch not found", exception.Message);
    }

    [Fact]
    public async Task Create_WithParentInDifferentConversation_Throws()
    {
        var conversationId = ConversationId.New();
        var otherConversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var parent = RootBranch(otherConversationId, "Parent");
        await repository.AddAsync(parent);
        var handler = new CreateBranchHandler(repository, new FixedTimeProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new CreateBranchCommand(
                    conversationId,
                    "Child",
                    string.Empty,
                    parent.Id)));

        Assert.Contains("different Conversation", exception.Message);
    }

    // --- update: reparenting (null leaves parent unchanged) -----------------

    [Fact]
    public async Task Update_WithoutParentBranchId_LeavesCurrentParentUnchanged()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var parent = RootBranch(conversationId, "Parent");
        var child = SubBranch(conversationId, "Child", parent.Id);
        await repository.AddAsync(parent);
        await repository.AddAsync(child);
        var handler = new UpdateBranchHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateBranchCommand(
                child.Id,
                "Child Renamed",
                "new description",
                BranchStatus.Active,
                ParentBranchId: null));

        Assert.NotNull(result);
        Assert.Equal("Child Renamed", result!.Name);
        Assert.Equal(parent.Id, result.ParentBranchId!.Value);

        var stored = await repository.GetAsync(child.Id);
        Assert.Equal(parent.Id, stored!.ParentBranchId!.Value);
    }

    [Fact]
    public async Task Update_ReparentToSelf_Throws()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var a = RootBranch(conversationId, "A");
        await repository.AddAsync(a);
        var handler = new UpdateBranchHandler(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new UpdateBranchCommand(
                    a.Id,
                    "A",
                    string.Empty,
                    BranchStatus.Active,
                    a.Id)));

        Assert.Contains("own parent", exception.Message);

        var stored = await repository.GetAsync(a.Id);
        Assert.Null(stored!.ParentBranchId);
    }

    [Fact]
    public async Task Update_DirectCycleReparentAUnderB_Throws()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var a = RootBranch(conversationId, "A");
        var b = SubBranch(conversationId, "B", a.Id);
        await repository.AddAsync(a);
        await repository.AddAsync(b);
        var handler = new UpdateBranchHandler(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new UpdateBranchCommand(
                    a.Id,
                    "A",
                    string.Empty,
                    BranchStatus.Active,
                    b.Id)));

        Assert.Contains("cycle", exception.Message);

        var stored = await repository.GetAsync(a.Id);
        Assert.Null(stored!.ParentBranchId);
    }

    [Fact]
    public async Task Update_DeeperCycleReparentAUnderC_Throws()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var a = RootBranch(conversationId, "A");
        var b = SubBranch(conversationId, "B", a.Id);
        var c = SubBranch(conversationId, "C", b.Id);
        await repository.AddAsync(a);
        await repository.AddAsync(b);
        await repository.AddAsync(c);
        var handler = new UpdateBranchHandler(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new UpdateBranchCommand(
                    a.Id,
                    "A",
                    string.Empty,
                    BranchStatus.Active,
                    c.Id)));

        Assert.Contains("cycle", exception.Message);

        var stored = await repository.GetAsync(a.Id);
        Assert.Null(stored!.ParentBranchId);
    }

    [Fact]
    public async Task Update_ReparentRootSiblingUnderAnotherRoot_IsAllowed()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var a = RootBranch(conversationId, "A");
        var b = RootBranch(conversationId, "B");
        await repository.AddAsync(a);
        await repository.AddAsync(b);
        var handler = new UpdateBranchHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateBranchCommand(
                b.Id,
                "B",
                string.Empty,
                BranchStatus.Active,
                a.Id));

        Assert.NotNull(result);
        Assert.Equal(a.Id, result!.ParentBranchId!.Value);

        var stored = await repository.GetAsync(b.Id);
        Assert.Equal(a.Id, stored!.ParentBranchId!.Value);
    }

    [Fact]
    public async Task Update_MissingBranch_ReturnsNull()
    {
        var repository = new InMemoryBranchRepository();
        var handler = new UpdateBranchHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateBranchCommand(
                BranchId.New(),
                "Ghost",
                string.Empty,
                BranchStatus.Active,
                ParentBranchId: null));

        Assert.Null(result);
    }

    // --- read: Get and List expose the parent link --------------------------

    [Fact]
    public async Task Get_SubBranch_ExposesParentBranchId()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var parent = RootBranch(conversationId, "Parent");
        var child = SubBranch(conversationId, "Child", parent.Id);
        await repository.AddAsync(parent);
        await repository.AddAsync(child);
        var handler = new GetBranchHandler(repository);

        var result = await handler.HandleAsync(
            new GetBranchQuery(child.Id));

        Assert.NotNull(result);
        Assert.Equal(parent.Id, result!.ParentBranchId!.Value);
    }

    [Fact]
    public async Task List_ByConversation_ReturnsSubBranchWithParent()
    {
        var conversationId = ConversationId.New();
        var repository = new InMemoryBranchRepository();
        var parent = RootBranch(conversationId, "Parent");
        var child = SubBranch(conversationId, "Child", parent.Id);
        await repository.AddAsync(parent);
        await repository.AddAsync(child);
        var handler = new ListBranchesHandler(repository);

        var result = await handler.HandleAsync(
            new ListBranchesQuery(conversationId));

        Assert.Equal(2, result.Count);
        var childResult = Assert.Single(result, b => b.Name == "Child");
        Assert.Equal(parent.Id, childResult.ParentBranchId!.Value);
        var rootResult = Assert.Single(result, b => b.Name == "Parent");
        Assert.Null(rootResult.ParentBranchId);
    }
}
