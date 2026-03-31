using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClawSharp.Lib.Runtime;

internal sealed class MemoryChunkEntity
{
    [Key]
    public string Id { get; set; } = null!;

    public string DocumentId { get; set; } = null!;

    public string Scope { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int ChunkIndex { get; set; }

    public string? MetadataJson { get; set; }

    /// <summary>
    /// 存储 float[] 的二进制表示 (Little Endian)
    /// </summary>
    public byte[] Vector { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
