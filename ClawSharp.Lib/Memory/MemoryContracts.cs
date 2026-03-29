using System.Text;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Memory;

public sealed record MemoryDocument(string Id, string Scope, string Source, string Content, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record MemoryChunk(string Id, string DocumentId, string Scope, string Content, int Index, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record EmbeddingVector(string Id, float[] Values);

public sealed record MemoryQuery(string Scope, string QueryText, int TopK = 5, IReadOnlyDictionary<string, string>? MetadataFilter = null);

public sealed record MemorySearchResult(string ChunkId, string DocumentId, string Content, double Score, IReadOnlyDictionary<string, string>? Metadata = null);

public interface IEmbeddingProvider
{
    Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<MemoryChunk> chunks, IReadOnlyList<EmbeddingVector> embeddings, CancellationToken cancellationToken = default);

    Task DeleteByScopeAsync(string scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySearchResult>> QueryAsync(MemoryQuery query, EmbeddingVector embedding, CancellationToken cancellationToken = default);
}

public interface IMemoryScopeResolver
{
    string Workspace(string workspaceId);

    string Agent(string workspaceId, string agentId);

    string Session(string workspaceId, string sessionId);
}

public interface IMemoryIndex
{
    Task IndexAsync(MemoryDocument document, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(MemoryQuery query, CancellationToken cancellationToken = default);
}

public sealed class DefaultMemoryScopeResolver : IMemoryScopeResolver
{
    public string Workspace(string workspaceId) => $"workspace:{workspaceId}";

    public string Agent(string workspaceId, string agentId) => $"agent:{workspaceId}:{agentId}";

    public string Session(string workspaceId, string sessionId) => $"session:{workspaceId}:{sessionId}";
}

public sealed class SimpleEmbeddingProvider(ClawOptions options) : IEmbeddingProvider
{
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

public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<string, (MemoryChunk Chunk, float[] Vector)> _entries = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertAsync(IReadOnlyList<MemoryChunk> chunks, IReadOnlyList<EmbeddingVector> embeddings, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < chunks.Count; i++)
        {
            _entries[chunks[i].Id] = (chunks[i], embeddings[i].Values);
        }

        return Task.CompletedTask;
    }

    public Task DeleteByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        var keys = _entries.Values.Where(x => x.Chunk.Scope == scope).Select(x => x.Chunk.Id).ToArray();
        foreach (var key in keys)
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

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

public sealed class MemoryIndex(ClawOptions options, IEmbeddingProvider embeddingProvider, IVectorStore vectorStore) : IMemoryIndex
{
    public async Task IndexAsync(MemoryDocument document, CancellationToken cancellationToken = default)
    {
        var chunks = Chunk(document, options.Memory.ChunkSize, options.Memory.ChunkOverlap);
        var embeddings = await embeddingProvider.EmbedAsync(chunks.Select(x => x.Content).ToArray(), cancellationToken).ConfigureAwait(false);
        await vectorStore.UpsertAsync(chunks, embeddings, cancellationToken).ConfigureAwait(false);
    }

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
