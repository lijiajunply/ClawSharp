using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClawSharp.Lib.Runtime;

internal sealed class ThreadSpaceEntityConfiguration : IEntityTypeConfiguration<ThreadSpaceEntity>
{
    public void Configure(EntityTypeBuilder<ThreadSpaceEntity> builder)
    {
        builder.ToTable("thread_spaces");
        builder.HasKey(x => x.ThreadSpaceId);

        builder.Property(x => x.ThreadSpaceId).HasColumnName("thread_space_id").HasMaxLength(128);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(256);
        builder.Property(x => x.BoundFolderPath).HasColumnName("bound_folder_path").HasMaxLength(1024).IsRequired(false);
        builder.Property(x => x.IsGlobal).HasColumnName("is_global");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ArchivedAt).HasColumnName("archived_at");

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.BoundFolderPath).IsUnique().HasFilter("bound_folder_path IS NOT NULL");
    }
}
