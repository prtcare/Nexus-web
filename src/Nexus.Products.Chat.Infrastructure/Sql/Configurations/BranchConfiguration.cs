using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Products.Chat.Domain.Branch;
using Nexus.Products.Chat.Domain.Conversation;
using Nexus.Products.Chat.Infrastructure.Sql.Conventions;

namespace Nexus.Products.Chat.Infrastructure.Sql.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branch", "session");

        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Id)
            .HasConversion(StronglyTypedIdConverters.BranchId)
            .ValueGeneratedNever();

        // ConversationId is the owning Conversation FK. SP1-D06 adds the optional
        // self-referencing ParentBranchId FK below (ADR-014's text claiming a self link
        // existed was previously wrong for the source; it is now true by design).
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(branch => branch.ConversationId)
            // Conversation is the owning parent - deleting a Conversation takes its Branches.
            .OnDelete(DeleteBehavior.Cascade);

        // SP1-D06 (Subchat recursion): optional self-referencing parent link. A branch may
        // point at a parent branch within the same Conversation (a thread/sub-chat); null
        // means root. Restrict (not Cascade) so deleting a parent Branch does not silently
        // take its sub-branches with it, and so the Conversation -> Branch cascade does not
        // create a second cascade path through the self link.
        builder.Property(branch => branch.ParentBranchId)
            .HasConversion(StronglyTypedIdConverters.BranchId);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(branch => branch.ParentBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(branch => branch.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(branch => branch.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(branch => branch.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(branch => branch.CreatedAt)
            .IsRequired();

        // ADR-014 hot-path index; also backs the FK.
        builder.HasIndex(branch => branch.ConversationId)
            .HasDatabaseName("IX_Branch_ConversationId");

        // SP1-D06: index for the self-referencing parent FK (walking a branch's ancestor
        // chain and listing direct sub-branches both filter on ParentBranchId).
        builder.HasIndex(branch => branch.ParentBranchId)
            .HasDatabaseName("IX_Branch_ParentBranchId");

        // The Branch domain entity exposes no Reference property (SP1-D06 did add
        // ParentBranchId, but Ref remains an EF shadow property). ADR-014's schema map still
        // gives session.Branch a BRN- ref for external tracing, so the column is an EF shadow
        // property fed by the same Seq identity pattern Workspace uses, mapped to no CLR property.
        builder.Property<int>("Seq")
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();

        var reference = builder.Property<string>("Ref")
            .IsRequired()
            .HasComputedColumnSql(
                "('BRN-' + RIGHT('00000000' + CAST([Seq] AS varchar(8)), 8))",
                stored: true)
            .ValueGeneratedOnAddOrUpdate();

        reference.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        reference.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex("Ref")
            .IsUnique()
            .HasDatabaseName("UQ_Branch_Ref");
    }
}
