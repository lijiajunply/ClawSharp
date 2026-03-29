using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClawSharp.Lib.Runtime;

internal sealed class SessionEntityConfiguration : IEntityTypeConfiguration<SessionEntity>
{
    public void Configure(EntityTypeBuilder<SessionEntity> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(128);
        builder.Property(x => x.ThreadSpaceId).HasColumnName("thread_space_id").HasMaxLength(128);
        builder.Property(x => x.WorkspaceRoot).HasColumnName("workspace_root").HasMaxLength(1024);
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");

        builder.HasIndex(x => x.ThreadSpaceId);
        builder.HasOne<ThreadSpaceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ThreadSpaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
