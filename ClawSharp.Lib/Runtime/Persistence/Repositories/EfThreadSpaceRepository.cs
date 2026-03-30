using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class EfThreadSpaceRepository(IDbContextFactory<ClawDbContext> dbContextFactory, ClawSqliteDatabaseInitializer initializer) : IThreadSpaceRepository
{
    public async Task CreateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.ThreadSpaces.Add(RuntimeEntityMapper.ToEntity(threadSpace));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThreadSpaceRecord?> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ThreadSpaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IsGlobal, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : RuntimeEntityMapper.ToRecord(entity);
    }

    public async Task<ThreadSpaceRecord?> GetAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ThreadSpaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ThreadSpaceId == threadSpaceId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : RuntimeEntityMapper.ToRecord(entity);
    }

    public async Task<ThreadSpaceRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ThreadSpaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == name, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : RuntimeEntityMapper.ToRecord(entity);
    }

    public async Task<ThreadSpaceRecord?> GetByBoundFolderPathAsync(string boundFolderPath, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ThreadSpaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.BoundFolderPath == boundFolderPath, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : RuntimeEntityMapper.ToRecord(entity);
    }

    public async Task<IReadOnlyList<ThreadSpaceRecord>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = context.ThreadSpaces.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(x => x.ArchivedAt == null);
        }

        return await query
            .OrderBy(x => x.IsGlobal ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ThreadSpaces.SingleOrDefaultAsync(x => x.ThreadSpaceId == threadSpace.ThreadSpaceId.Value, cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            entity.Name = threadSpace.Name;
            entity.BoundFolderPath = threadSpace.BoundFolderPath;
            entity.IsGlobal = threadSpace.IsGlobal;
            entity.ArchivedAt = threadSpace.ArchivedAt;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ArchiveAsync(ThreadSpaceId threadSpaceId, DateTimeOffset archivedAt, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.ThreadSpaces.SingleOrDefaultAsync(x => x.ThreadSpaceId == threadSpaceId.Value, cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            entity.ArchivedAt = archivedAt;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}