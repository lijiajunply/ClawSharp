using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;

namespace ClawSharp.Lib.Tests;

public sealed class MemoryTests
{
    [Fact]
    public async Task MemoryIndex_KeepsScopesSeparated()
    {
        var options = new ClawOptions();
        var provider = new SimpleEmbeddingProvider(options);
        var store = new InMemoryVectorStore();
        var index = new MemoryIndex(options, provider, store);

        await index.IndexAsync(new MemoryDocument("doc-1", "workspace:a", "file", "alpha beta gamma"));
        await index.IndexAsync(new MemoryDocument("doc-2", "workspace:b", "file", "delta epsilon zeta"));

        var first = await index.SearchAsync(new MemoryQuery("workspace:a", "alpha"));
        var second = await index.SearchAsync(new MemoryQuery("workspace:b", "delta"));

        Assert.All(first, item => Assert.Equal("doc-1", item.DocumentId));
        Assert.All(second, item => Assert.Equal("doc-2", item.DocumentId));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    public void SqliteVssVectorStore_NormalizeTopK_ClampsToPositiveValue(int input, int expected)
    {
        Assert.Equal(expected, SqliteVssVectorStore.NormalizeTopK(input));
    }
}
