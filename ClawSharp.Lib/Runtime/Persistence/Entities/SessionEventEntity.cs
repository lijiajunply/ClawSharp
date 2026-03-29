namespace ClawSharp.Lib.Runtime;

internal sealed class SessionEventEntity
{
    public required string EventId { get; init; }

    public required string SessionId { get; init; }

    public required string TurnId { get; init; }

    public required string EventType { get; init; }

    public required string PayloadJson { get; init; }

    public int SequenceNo { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
