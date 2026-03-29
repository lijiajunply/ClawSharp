namespace ClawSharp.Lib.Runtime;

internal sealed class MessageEntity
{
    public required string MessageId { get; init; }

    public required string SessionId { get; init; }

    public required string TurnId { get; init; }

    public PromptMessageRole Role { get; init; }

    public required string Content { get; init; }

    public string? Name { get; init; }

    public string? ToolCallId { get; init; }

    public string? BlocksJson { get; init; }

    public int SequenceNo { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
