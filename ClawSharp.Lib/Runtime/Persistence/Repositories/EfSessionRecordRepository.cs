using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class EfSessionRecordRepository(IDbContextFactory<ClawDbContext> dbContextFactory, ClawSqliteDatabaseInitializer initializer) : ISessionRecordRepository
{
    public async Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Sessions.Add(RuntimeEntityMapper.ToEntity(session));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SessionId == sessionId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : RuntimeEntityMapper.ToRecord(entity);
    }

    public async Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var results = await context.Sessions.AsNoTracking()
            .Where(x => x.Status == SessionStatus.Created || x.Status == SessionStatus.Running || x.Status == SessionStatus.WaitingForApproval)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return results.OrderByDescending(x => x.StartedAt).ToList();
    }

    public async Task<IReadOnlyList<SessionRecord>> ListByThreadSpaceAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var results = await context.Sessions.AsNoTracking()
            .Where(x => x.ThreadSpaceId == threadSpaceId.Value)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return results.OrderByDescending(x => x.StartedAt).ToList();
    }

    public async Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Sessions.SingleAsync(x => x.SessionId == sessionId.Value, cancellationToken).ConfigureAwait(false);
        entity.Status = status;
        entity.EndedAt = endedAt;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
