namespace ClawSharp.Lib.Runtime;

internal interface ISessionEventRepository
{
    Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, System.Text.Json.JsonElement payload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task DeleteBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}
