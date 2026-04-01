namespace ClawSharp.Lib.Runtime;

internal interface ISessionRecordRepository
{
    Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default);

    Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionRecord>> ListByThreadSpaceAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt, CancellationToken cancellationToken = default);

    Task UpdateOutputLanguageAsync(SessionId sessionId, string? outputLanguage, CancellationToken cancellationToken = default);
}
