using SmartComponents.LocalEmbeddings;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Memory;

/// <summary>
/// 基于 SmartComponents.LocalEmbeddings 的本地向量生成实现。
/// </summary>
public sealed class LocalEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly ClawOptions _options;
    private readonly LocalEmbedder _embedder;

    public LocalEmbeddingProvider(ClawOptions options)
    {
        _options = options;
        // 注意：SmartComponents.LocalEmbeddings 默认会下载并使用 all-MiniLM-L6-v2 模型
        // 如果提供了特定的路径，可以使用特定的模型。目前使用默认。
        _embedder = new LocalEmbedder();
    }

    public int Dimensions => 384; // all-MiniLM-L6-v2 的标准维度

    public Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return Task.FromResult<IReadOnlyList<EmbeddingVector>>(Array.Empty<EmbeddingVector>());

        var results = new List<EmbeddingVector>();
        for (int i = 0; i < texts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var embedding = _embedder.Embed(texts[i]);
            results.Add(new EmbeddingVector(i.ToString(), embedding.Values.ToArray()));
        }

        return Task.FromResult<IReadOnlyList<EmbeddingVector>>(results);
    }

    public void Dispose()
    {
        _embedder.Dispose();
    }
}
