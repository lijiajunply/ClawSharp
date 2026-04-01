using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClawSharp.Lib.Tests;

public class RagIntegrationTests
{
    [Fact]
    public async Task SqliteVss_Should_Persist_And_Retrieve_Vectors()
    {
        // 1. Setup
        var services = new ServiceCollection();
        var options = new ClawOptions();
        options.Runtime.WorkspaceRoot = Path.GetTempPath();
        options.Runtime.DataPath = "clawsharp_test_" + Guid.NewGuid().ToString("N");
        options.Embedding.Dimensions = 384; // 模拟维度

        services.AddSingleton(options);
        services.AddLogging();
        services.AddSingleton<IVectorStore, SqliteVssVectorStore>();
        
        var sp = services.BuildServiceProvider();
        var vectorStore = sp.GetRequiredService<IVectorStore>();

        var scope = "test-scope";
        var chunks = new List<MemoryChunk>
        {
            new MemoryChunk("c1", "d1", scope, "The quick brown fox jumps over the lazy dog", 0)
        };
        var vectors = new List<EmbeddingVector>
        {
            new EmbeddingVector("c1", new float[384]) // 零向量用于测试基础存储
        };

        // 2. Act: Upsert
        await vectorStore.UpsertAsync(chunks, vectors);

        // 3. Assert: Query
        var query = new MemoryQuery(scope, "fox", TopK: 1);
        var results = await vectorStore.QueryAsync(query, new EmbeddingVector("q", new float[384]));

        Assert.NotEmpty(results);
        Assert.Equal("c1", results.First().ChunkId);

        // 4. Cleanup
        if (vectorStore is IDisposable d) d.Dispose();
        if (Directory.Exists(Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath)))
        {
            Directory.Delete(Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath), true);
        }
    }

    [Fact]
    public async Task SqliteVss_Querying_Empty_Scope_Returns_Empty_Without_Crashing()
    {
        var services = new ServiceCollection();
        var options = new ClawOptions();
        options.Runtime.WorkspaceRoot = Path.GetTempPath();
        options.Runtime.DataPath = "clawsharp_test_" + Guid.NewGuid().ToString("N");
        options.Embedding.Dimensions = 384;

        services.AddSingleton(options);
        services.AddLogging();
        services.AddSingleton<IVectorStore, SqliteVssVectorStore>();

        var sp = services.BuildServiceProvider();
        var vectorStore = sp.GetRequiredService<IVectorStore>();

        var results = await vectorStore.QueryAsync(
            new MemoryQuery("empty-scope", "hello", TopK: 5),
            new EmbeddingVector("q", new float[384]));

        Assert.Empty(results);

        if (vectorStore is IDisposable d) d.Dispose();
        if (Directory.Exists(Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath)))
        {
            Directory.Delete(Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath), true);
        }
    }

    [Fact]
    public async Task LocalEmbeddingProvider_Should_Generate_Vectors()
    {
        var options = new ClawOptions();
        using var provider = new LocalEmbeddingProvider(options);

        var result = await provider.EmbedAsync(new[] { "Hello world" });

        Assert.Single(result);
        Assert.Equal(384, result[0].Values.Length);
    }
}
