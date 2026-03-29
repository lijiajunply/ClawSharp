using ClawSharp.Lib.Providers;

namespace ClawSharp.Lib.Runtime;

internal interface IPromptMessageRepository
{
    Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name, string? toolCallId,
        CancellationToken cancellationToken = default);

    Task<PromptMessage> AppendBlocksAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, IReadOnlyList<ModelContentBlock> blocks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task DeleteBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}
