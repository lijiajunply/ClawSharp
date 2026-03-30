namespace ClawSharp.Lib.Runtime;

internal sealed class ThreadSpaceEntity
{
    public required string ThreadSpaceId { get; init; }

    public required string Name { get; set; }

    public string? BoundFolderPath { get; set; }

    public bool IsGlobal { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ArchivedAt { get; set; }
}
