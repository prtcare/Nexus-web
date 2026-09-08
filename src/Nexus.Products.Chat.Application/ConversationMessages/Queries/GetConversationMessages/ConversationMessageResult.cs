using Nexus.Products.Chat.Domain.ConversationMessage;

namespace Nexus.Products.Chat.Application.ConversationMessages.Queries.GetConversationMessages;

public sealed record ConversationMessageResult(
    ConversationMessageId MessageId,
    ConversationMessageRole Role,
    string Content,
    DateTimeOffset CreatedOn,
    string? IntelligenceTurnId = null);