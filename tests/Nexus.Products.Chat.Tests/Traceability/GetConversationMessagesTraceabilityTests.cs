using Nexus.Products.Chat.Application.ConversationMessages.Queries.GetConversationMessages;
using Nexus.Products.Chat.Domain.Conversation;
using Nexus.Products.Chat.Domain.ConversationMessage;
using ConversationMessage = Nexus.Products.Chat.Domain.ConversationMessage.ConversationMessage;
using Xunit;

namespace Nexus.Products.Chat.Tests.Traceability;

public sealed class GetConversationMessagesTraceabilityTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_surfaces_intelligence_turn_id()
    {
        var conversationId = ConversationId.New();
        var assistant = new ConversationMessage(
            ConversationMessageId.New(),
            conversationId,
            ConversationMessageRole.Assistant,
            "Reply text",
            FixedNow);
        assistant.AttachIntelligenceTurnId("0123456789abcdef0123456789abcdef");

        var repository = new StubConversationMessageRepository(conversationId, assistant);
        var handler = new GetConversationMessagesHandler(repository);

        var result = await handler.HandleAsync(
            new GetConversationMessagesQuery(conversationId),
            CancellationToken.None);

        var message = Assert.Single(result);
        Assert.Equal("0123456789abcdef0123456789abcdef", message.IntelligenceTurnId);
        Assert.Equal("Reply text", message.Content);
    }

    [Fact]
    public async Task HandleAsync_returns_null_turn_id_for_untraced_message()
    {
        var conversationId = ConversationId.New();
        var user = new ConversationMessage(
            ConversationMessageId.New(),
            conversationId,
            ConversationMessageRole.User,
            "Prompt text",
            FixedNow);

        var repository = new StubConversationMessageRepository(conversationId, user);
        var handler = new GetConversationMessagesHandler(repository);

        var result = await handler.HandleAsync(
            new GetConversationMessagesQuery(conversationId),
            CancellationToken.None);

        var message = Assert.Single(result);
        Assert.Null(message.IntelligenceTurnId);
    }

    private sealed class StubConversationMessageRepository : IConversationMessageRepository
    {
        private readonly ConversationId _conversationId;
        private readonly IReadOnlyList<ConversationMessage> _messages;

        public StubConversationMessageRepository(ConversationId conversationId, params ConversationMessage[] messages)
        {
            _conversationId = conversationId;
            _messages = messages;
        }

        public Task AddAsync(ConversationMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(ConversationMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ConversationMessage?> GetAsync(ConversationMessageId id, CancellationToken cancellationToken = default)
            => Task.FromResult(_messages.FirstOrDefault(message => message.Id == id));

        public Task<IReadOnlyList<ConversationMessage>> ListByConversationAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken = default)
        {
            if (conversationId != _conversationId)
            {
                return Task.FromResult<IReadOnlyList<ConversationMessage>>([]);
            }

            return Task.FromResult(_messages);
        }
    }
}
