using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClawSharp.Lib.Runtime;

internal sealed class MessageEntityConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(x => x.MessageId);

        builder.Property(x => x.MessageId).HasColumnName("message_id").HasMaxLength(128);
        builder.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(x => x.TurnId).HasColumnName("turn_id").HasMaxLength(128);
        builder.Property(x => x.Role).HasColumnName("role");
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(10_000_000);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(x => x.ToolCallId).HasColumnName("tool_call_id").HasMaxLength(128);
        builder.Property(x => x.BlocksJson).HasColumnName("blocks_json").HasMaxLength(10_000_000);
        builder.Property(x => x.SequenceNo).HasColumnName("sequence_no");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => new { x.SessionId, x.SequenceNo });
        builder.HasOne<SessionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .HasPrincipalKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
