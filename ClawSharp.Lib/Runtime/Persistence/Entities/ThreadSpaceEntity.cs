namespace ClawSharp.Lib.Runtime;

internal sealed class ThreadSpaceEntity
{
    public required string ThreadSpaceId { get; init; }

    public required string Name { get; init; }

    public required string BoundFolderPath { get; set; }

    public bool IsInit { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ArchivedAt { get; init; }
}
