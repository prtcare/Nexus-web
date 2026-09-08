using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Products.Chat.Domain.ConversationMessage;
using Nexus.Products.Chat.Infrastructure.Sql.Conventions;

namespace Nexus.Products.Chat.Infrastructure.Sql.Configurations;

public sealed class ConversationMessageConfiguration
    : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("ConversationMessage", "conversation");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasConversion(StronglyTypedIdConverters.ConversationMessageId)
            .ValueGeneratedNever();

        builder.Property(message => message.ConversationId)
            .HasConversion(StronglyTypedIdConverters.ConversationId)
            .IsRequired();

        builder.Property(message => message.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(message => message.CreatedOn)
            .IsRequired();

        // WU-02 turn/evidence traceability: nullable Intelligence turn id column. TurnId is
        // Guid.ToString("N") (32 chars) on the Intelligence side; 64 leaves headroom without
        // an unbounded column. Null for pre-traceability and non-turn rows.
        builder.Property(message => message.IntelligenceTurnId)
            .HasColumnType("nvarchar(64)");

        // ADR-014 schema map: conversation.ConversationMessage is a child aggregate with no
        // Ref (no Seq identity column either). Hot query path is messages-of-a-conversation
        // ordered by creation.
        builder.HasIndex(
                message => new { message.ConversationId, message.CreatedOn })
            .HasDatabaseName("IX_ConversationMessage_ConversationId_CreatedOn");
    }
}
