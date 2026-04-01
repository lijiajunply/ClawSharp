namespace ClawSharp.Lib.Runtime;

internal sealed class SessionEntity
{
    public required string SessionId { get; init; }

    public required string AgentId { get; init; }

    public required string ThreadSpaceId { get; set; }

    public required string WorkspaceRoot { get; set; }

    public SessionStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string? OutputLanguageOverride { get; set; }
}
