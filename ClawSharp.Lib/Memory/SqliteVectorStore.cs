using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClawSharp.Lib.Runtime;

namespace ClawSharp.Lib.Memory;

/// <summary>
/// 基于 SQLite 的向量存储实现，支持 BLOB 向量存储。
/// </summary>
internal sealed class SqliteVectorStore(IDbContextFactory<ClawDbContext> dbContextFactory) : IVectorStore
{
    /// <inheritdoc />
    public async Task UpsertAsync(IReadOnlyList<MemoryChunk> chunks, IReadOnlyList<EmbeddingVector> embeddings, CancellationToken cancellationToken = default)
    {
        using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = new List<MemoryChunkEntity>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = embeddings[i];

            entities.Add(new MemoryChunkEntity
            {
                Id = chunk.Id,
                DocumentId = chunk.DocumentId,
                Scope = chunk.Scope,
                Content = chunk.Content,
                ChunkIndex = chunk.Index,
                MetadataJson = chunk.Metadata != null ? JsonSerializer.Serialize(chunk.Metadata) : null,
                Vector = FloatToByte(embedding.Values),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        // 使用批量 Upsert 逻辑 (通过先删除后插入实现，或 EF Core 提供的等效操作)
        var ids = entities.Select(e => e.Id).ToList();
        var existing = await db.MemoryChunks.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.MemoryChunks.RemoveRange(existing);
        
        await db.MemoryChunks.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existing = await db.MemoryChunks.Where(x => x.Scope == scope).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.MemoryChunks.RemoveRange(existing);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemorySearchResult>> QueryAsync(MemoryQuery query, EmbeddingVector embedding, CancellationToken cancellationToken = default)
    {
        using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // 基础过滤：Scope 和元数据（内存中过滤）
        var candidates = await db.MemoryChunks
            .Where(x => x.Scope == query.Scope)
            .Select(x => new { x.Id, x.DocumentId, x.Content, x.Vector, x.MetadataJson })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = candidates
            .Select(x => new
            {
                x.Id,
                x.DocumentId,
                x.Content,
                x.MetadataJson,
                Score = CosineSimilarity(embedding.Values, ByteToFloat(x.Vector))
            })
            .Where(x => x.Score > 0.1) // 简单阈值过滤
            .OrderByDescending(x => x.Score)
            .Take(query.TopK)
            .Select(x => new MemorySearchResult(
                x.Id,
                x.DocumentId,
                x.Content,
                x.Score,
                x.MetadataJson != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(x.MetadataJson) : null))
            .ToList();

        return results;
    }

    private static byte[] FloatToByte(float[] values)
    {
        var result = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, result, 0, result.Length);
        return result;
    }

    private static float[] ByteToFloat(byte[] bytes)
    {
        var result = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length) return 0;
        
        var dot = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
        }

        return dot;
    }
}
