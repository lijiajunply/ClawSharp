using ClawSharp.Lib.Providers;
using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class EfPromptMessageRepository(IDbContextFactory<ClawDbContext> dbContextFactory, ClawSqliteDatabaseInitializer initializer) : IPromptMessageRepository
{
    public async Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name, string? toolCallId,
        CancellationToken cancellationToken = default)
    {
        var blocks = JsonSessionSerializerHelper.ParseBlocks(null, role, content, name, toolCallId);
        return await AppendBlocksAsync(sessionId, turnId, role, blocks, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PromptMessage> AppendBlocksAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, IReadOnlyList<ModelContentBlock> blocks,
        CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var sequenceNo = await context.Messages
            .Where(x => x.SessionId == sessionId.Value)
            .Select(x => (int?)x.SequenceNo)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var message = new PromptMessage(MessageId.New(), sessionId, turnId, role, blocks, sequenceNo + 1, DateTimeOffset.UtcNow);
        context.Messages.Add(RuntimeEntityMapper.ToEntity(message));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return message;
    }

    public async Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Messages.AsNoTracking()
            .Where(x => x.SessionId == sessionId.Value)
            .OrderBy(x => x.SequenceNo)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        initializer.EnsureInitialized();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var messages = await context.Messages.Where(x => x.SessionId == sessionId.Value).ToListAsync(cancellationToken).ConfigureAwait(false);
        context.Messages.RemoveRange(messages);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
