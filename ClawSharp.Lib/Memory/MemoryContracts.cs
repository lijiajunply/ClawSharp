using System.Text;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Memory;

/// <summary>
/// 表示待写入记忆索引的原始文档。
/// </summary>
/// <param name="Id">文档标识。</param>
/// <param name="Scope">记忆 scope。</param>
/// <param name="Source">文档来源描述。</param>
/// <param name="Content">文档正文。</param>
/// <param name="Metadata">可选元数据。</param>
public sealed record MemoryDocument(string Id, string Scope, string Source, string Content, IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// 表示切块后的记忆片段。
/// </summary>
/// <param name="Id">chunk 标识。</param>
/// <param name="DocumentId">所属文档标识。</param>
/// <param name="Scope">记忆 scope。</param>
/// <param name="Content">chunk 内容。</param>
/// <param name="Index">chunk 顺序号。</param>
/// <param name="Metadata">可选元数据。</param>
public sealed record MemoryChunk(string Id, string DocumentId, string Scope, string Content, int Index, IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// 表示 embedding 向量。
/// </summary>
/// <param name="Id">向量标识。</param>
/// <param name="Values">向量值。</param>
public sealed record EmbeddingVector(string Id, float[] Values);

/// <summary>
/// 表示一次记忆检索请求。
/// </summary>
/// <param name="Scope">检索的 scope。</param>
/// <param name="QueryText">查询文本。</param>
/// <param name="TopK">最多返回结果数。</param>
/// <param name="MetadataFilter">可选元数据过滤条件。</param>
public sealed record MemoryQuery(string Scope, string QueryText, int TopK = 5, IReadOnlyDictionary<string, string>? MetadataFilter = null);

/// <summary>
/// 表示一次记忆检索命中的结果。
/// </summary>
/// <param name="ChunkId">命中的 chunk 标识。</param>
/// <param name="DocumentId">所属文档标识。</param>
/// <param name="Content">命中的内容。</param>
/// <param name="Score">相关性分数。</param>
/// <param name="Metadata">相关元数据。</param>
public sealed record MemorySearchResult(string ChunkId, string DocumentId, string Content, double Score, IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// embedding 提供者抽象。
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// 为一组文本生成 embedding。
    /// </summary>
    /// <param name="texts">输入文本集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入顺序对应的向量集合。</returns>
    Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向量维度。
    /// </summary>
    int Dimensions { get; }
}

/// <summary>
/// 向量存储抽象。
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// 批量写入或更新记忆 chunk 及其向量。
    /// </summary>
    /// <param name="chunks">chunk 集合。</param>
    /// <param name="embeddings">与 chunk 一一对应的向量集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpsertAsync(IReadOnlyList<MemoryChunk> chunks, IReadOnlyList<EmbeddingVector> embeddings, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定 scope 下的全部向量数据。
    /// </summary>
    /// <param name="scope">要删除的 scope。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteByScopeAsync(string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用给定 embedding 查询相似内容。
    /// </summary>
    /// <param name="query">查询描述。</param>
    /// <param name="embedding">查询 embedding。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按相关性排序的结果列表。</returns>
    Task<IReadOnlyList<MemorySearchResult>> QueryAsync(MemoryQuery query, EmbeddingVector embedding, CancellationToken cancellationToken = default);
}

/// <summary>
/// 根据 workspace、agent、session 生成记忆 scope 名称的抽象。
/// </summary>
public interface IMemoryScopeResolver
{
    /// <summary>
    /// 生成 workspace 级 scope 名称。
    /// </summary>
    /// <param name="workspaceId">workspace 标识。</param>
    /// <returns>scope 名称。</returns>
    string Workspace(string workspaceId);

    /// <summary>
    /// 生成 agent 级 scope 名称。
    /// </summary>
    /// <param name="workspaceId">workspace 标识。</param>
    /// <param name="agentId">agent 标识。</param>
    /// <returns>scope 名称。</returns>
    string Agent(string workspaceId, string agentId);

    /// <summary>
    /// 生成 session 级 scope 名称。
    /// </summary>
    /// <param name="workspaceId">workspace 标识。</param>
    /// <param name="sessionId">session 标识。</param>
    /// <returns>scope 名称。</returns>
    string Session(string workspaceId, string sessionId);
}

/// <summary>
/// 记忆索引抽象。
/// </summary>
public interface IMemoryIndex
{
    /// <summary>
    /// 对一个文档进行切块、embedding 并写入向量存储。
    /// </summary>
    /// <param name="document">待索引文档。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task IndexAsync(MemoryDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜索记忆索引。
    /// </summary>
    /// <param name="query">查询描述。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按相关性排序的结果列表。</returns>
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(MemoryQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的 scope 解析器。
/// </summary>
public sealed class DefaultMemoryScopeResolver : IMemoryScopeResolver
{
    /// <inheritdoc />
    public string Workspace(string workspaceId) => $"workspace:{workspaceId}";

    /// <inheritdoc />
    public string Agent(string workspaceId, string agentId) => $"agent:{workspaceId}:{agentId}";

    /// <inheritdoc />
    public string Session(string workspaceId, string sessionId) => $"session:{workspaceId}:{sessionId}";
}

/// <summary>
/// 一个轻量的默认 embedding 实现，适合测试和本地演示。
/// </summary>
/// <param name="options">embedding 维度配置来源。</param>
public sealed class SimpleEmbeddingProvider(ClawOptions options) : IEmbeddingProvider
{
    /// <inheritdoc />
    public int Dimensions => options.Embedding.Dimensions;

    /// <inheritdoc />
    public Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var vectors = texts.Select((text, index) => new EmbeddingVector(index.ToString(), BuildVector(text, options.Embedding.Dimensions))).ToArray();
        return Task.FromResult<IReadOnlyList<EmbeddingVector>>(vectors);
    }

    private static float[] BuildVector(string text, int dimensions)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var values = new float[dimensions];
        for (var i = 0; i < bytes.Length; i++)
        {
            values[i % dimensions] += bytes[i] / 255f;
        }

        var norm = Math.Sqrt(values.Sum(x => x * x));
        if (norm > 0)
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = (float)(values[i] / norm);
            }
        }

        return values;
    }
}

/// <summary>
/// 仅驻留内存的向量存储实现。
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<string, (MemoryChunk Chunk, float[] Vector)> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task UpsertAsync(IReadOnlyList<MemoryChunk> chunks, IReadOnlyList<EmbeddingVector> embeddings, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < chunks.Count; i++)
        {
            _entries[chunks[i].Id] = (chunks[i], embeddings[i].Values);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        var keys = _entries.Values.Where(x => x.Chunk.Scope == scope).Select(x => x.Chunk.Id).ToArray();
        foreach (var key in keys)
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MemorySearchResult>> QueryAsync(MemoryQuery query, EmbeddingVector embedding, CancellationToken cancellationToken = default)
    {
        var results = _entries.Values
            .Where(x => x.Chunk.Scope == query.Scope)
            .Where(x => MatchesFilter(x.Chunk.Metadata, query.MetadataFilter))
            .Select(x => new MemorySearchResult(
                x.Chunk.Id,
                x.Chunk.DocumentId,
                x.Chunk.Content,
                CosineSimilarity(x.Vector, embedding.Values),
                x.Chunk.Metadata))
            .OrderByDescending(x => x.Score)
            .Take(query.TopK)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MemorySearchResult>>(results);
    }

    private static bool MatchesFilter(IReadOnlyDictionary<string, string>? metadata, IReadOnlyDictionary<string, string>? filter)
    {
        if (filter is null || filter.Count == 0)
        {
            return true;
        }

        if (metadata is null)
        {
            return false;
        }

        return filter.All(pair => metadata.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var dot = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
        }

        return dot;
    }
}

/// <summary>
/// 默认的记忆索引实现，负责切块、embedding 和向量查询。
/// </summary>
/// <param name="options">切块配置来源。</param>
/// <param name="embeddingProvider">embedding 提供者。</param>
/// <param name="vectorStore">向量存储。</param>
public sealed class MemoryIndex(ClawOptions options, IEmbeddingProvider embeddingProvider, IVectorStore vectorStore) : IMemoryIndex
{
    /// <inheritdoc />
    public async Task IndexAsync(MemoryDocument document, CancellationToken cancellationToken = default)
    {
        var chunks = Chunk(document, options.Memory.ChunkSize, options.Memory.ChunkOverlap);
        var embeddings = await embeddingProvider.EmbedAsync(chunks.Select(x => x.Content).ToArray(), cancellationToken).ConfigureAwait(false);
        await vectorStore.UpsertAsync(chunks, embeddings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(MemoryQuery query, CancellationToken cancellationToken = default)
    {
        var embedding = (await embeddingProvider.EmbedAsync([query.QueryText], cancellationToken).ConfigureAwait(false)).Single();
        return await vectorStore.QueryAsync(query, embedding, cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<MemoryChunk> Chunk(MemoryDocument document, int size, int overlap)
    {
        var segments = new List<MemoryChunk>();
        var content = document.Content ?? string.Empty;
        var start = 0;
        var index = 0;

        while (start < content.Length)
        {
            var length = Math.Min(size, content.Length - start);
            var chunkContent = content.Substring(start, length);
            segments.Add(new MemoryChunk(
                $"{document.Id}:{index}",
                document.Id,
                document.Scope,
                chunkContent,
                index,
                document.Metadata));

            index++;
            if (start + length >= content.Length)
            {
                break;
            }

            start += Math.Max(1, size - overlap);
        }

        return segments;
    }
}
