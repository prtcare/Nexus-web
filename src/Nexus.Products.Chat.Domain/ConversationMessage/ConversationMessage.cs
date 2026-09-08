using Nexus.Products.Chat.Domain.Common;
using Nexus.Products.Chat.Domain.Conversation;

namespace Nexus.Products.Chat.Domain.ConversationMessage;

public sealed class ConversationMessage
    : AggregateRoot<ConversationMessageId>
{
    // Recorded Domain exception (ADR-014 Stage 2a, same precedent as Workspace.Reference in
    // Stage 1b): EF Core cannot bind this entity's public constructor because its
    // DateTimeOffset? createdOn parameter cannot bind to the non-nullable CreatedOn
    // property. This private parameterless constructor is for EF materialization only -
    // EF sets the get-only backing fields directly and it is never used by application
    // code. No other constructor is added; the public constructor is untouched.
    private ConversationMessage()
        : base(default)
    {
    }

    public ConversationMessage(
        ConversationMessageId id,
        ConversationId conversationId,
        ConversationMessageRole role,
        string content,
        DateTimeOffset? createdOn = null)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        ConversationId = conversationId;
        Role = role;
        Content = content.Trim();
        CreatedOn = createdOn ?? DateTimeOffset.UtcNow;
    }

    public ConversationId ConversationId { get; }

    public ConversationMessageRole Role { get; }

    public string Content { get; }

    public DateTimeOffset CreatedOn { get; }

    // WU-02 turn/evidence traceability: the Intelligence turn id that produced this
    // message. Nullable and optional so existing message creation is unaffected; it is
    // attached only after a turn has run (see SendChatHandler). Null for historical rows
    // and for messages that were never the output/trigger of an Intelligence turn.
    public string? IntelligenceTurnId { get; private set; }

    public void AttachIntelligenceTurnId(string intelligenceTurnId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intelligenceTurnId);

        IntelligenceTurnId = intelligenceTurnId.Trim();
    }
}