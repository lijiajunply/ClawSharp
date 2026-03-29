using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClawSharp.Lib.Runtime;

internal sealed class SessionEventEntityConfiguration : IEntityTypeConfiguration<SessionEventEntity>
{
    public void Configure(EntityTypeBuilder<SessionEventEntity> builder)
    {
        builder.ToTable("session_events");
        builder.HasKey(x => x.EventId);

        builder.Property(x => x.EventId).HasColumnName("event_id").HasMaxLength(128);
        builder.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(x => x.TurnId).HasColumnName("turn_id").HasMaxLength(128);
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(128);
        builder.Property(x => x.PayloadJson).HasColumnName("payload").HasMaxLength(10_000_000);
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
