using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class EfSessionEventRepository(IDbContextFactory<ClawDbContext> dbContextFactory, ClawSqliteDatabaseInitializer initializer) : ISessionEventRepository
{
    public async Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, System.Text.Json.JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var sequenceNo = await context.SessionEvents
            .Where(x => x.SessionId == sessionId.Value)
            .Select(x => (int?)x.SequenceNo)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var sessionEvent = new SessionEvent(EventId.New(), sessionId, turnId, eventType, payload, sequenceNo + 1, DateTimeOffset.UtcNow);
        context.SessionEvents.Add(RuntimeEntityMapper.ToEntity(sessionEvent));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sessionEvent;
    }

    public async Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.SessionEvents.AsNoTracking()
            .Where(x => x.SessionId == sessionId.Value)
            .OrderBy(x => x.SequenceNo)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
