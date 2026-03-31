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

    /// <summary>
    /// 使用当前应用配置创建本地向量提供器。
    /// </summary>
    /// <param name="options">应用配置。</param>
    public LocalEmbeddingProvider(ClawOptions options)
    {
        _options = options;
        // 注意：SmartComponents.LocalEmbeddings 默认会下载并使用 all-MiniLM-L6-v2 模型
        // 如果提供了特定的路径，可以使用特定的模型。目前使用默认。
        _embedder = new LocalEmbedder();
    }

    /// <summary>
    /// 当前本地模型输出向量的维度。
    /// </summary>
    public int Dimensions => 384; // all-MiniLM-L6-v2 的标准维度

    /// <summary>
    /// 为输入文本批量生成本地向量。
    /// </summary>
    /// <param name="texts">待嵌入的文本集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入顺序一致的向量列表。</returns>
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

    /// <summary>
    /// 释放底层本地嵌入模型占用的资源。
    /// </summary>
    public void Dispose()
    {
        _embedder.Dispose();
    }
}
