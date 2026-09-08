using Nexus.Products.Chat.Domain.Conversation;
using Nexus.Products.Chat.Domain.ConversationMessage;
using ConversationMessage = Nexus.Products.Chat.Domain.ConversationMessage.ConversationMessage;
using Xunit;

namespace Nexus.Products.Chat.Tests.Traceability;

public sealed class ConversationMessageTraceabilityTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);

    private static ConversationMessage CreateMessage()
        => new(
            ConversationMessageId.New(),
            ConversationId.New(),
            ConversationMessageRole.Assistant,
            "Hello there",
            FixedNow);

    [Fact]
    public void New_message_has_no_intelligence_turn_id()
    {
        var message = CreateMessage();

        Assert.Null(message.IntelligenceTurnId);
    }

    [Fact]
    public void Attach_intelligence_turn_id_retains_turn_id()
    {
        var message = CreateMessage();

        message.AttachIntelligenceTurnId("0123456789abcdef0123456789abcdef");

        Assert.Equal("0123456789abcdef0123456789abcdef", message.IntelligenceTurnId);
    }

    [Fact]
    public void Attach_intelligence_turn_id_trims_whitespace()
    {
        var message = CreateMessage();

        message.AttachIntelligenceTurnId("  abc-def-123  ");

        Assert.Equal("abc-def-123", message.IntelligenceTurnId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Attach_intelligence_turn_id_rejects_blank(string? turnId)
    {
        var message = CreateMessage();

        Assert.ThrowsAny<ArgumentException>(() => message.AttachIntelligenceTurnId(turnId!));
    }
}
