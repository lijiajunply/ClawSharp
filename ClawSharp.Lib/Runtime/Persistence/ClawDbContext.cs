using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class ClawDbContext(DbContextOptions<ClawDbContext> options) : DbContext(options)
{
    public DbSet<ThreadSpaceEntity> ThreadSpaces => Set<ThreadSpaceEntity>();

    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<SessionEventEntity> SessionEvents => Set<SessionEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClawDbContext).Assembly);
    }
}
