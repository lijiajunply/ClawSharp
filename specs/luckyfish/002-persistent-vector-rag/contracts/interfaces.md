# Contracts: Persistent Vector Store and Automated RAG

## Interfaces

### IEmbeddingProvider
```csharp
public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    Task<float[][]> GenerateBatchEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);
    int Dimension { get; }
}
```

### IVectorStore (Refined)
```csharp
public interface IVectorStore
{
    Task UpsertAsync(string ns, KnowledgeChunk chunk, CancellationToken ct = default);
    Task<IEnumerable<KnowledgeChunk>> SearchAsync(string ns, float[] queryVector, int limit = 5, CancellationToken ct = default);
    Task DeleteNamespaceAsync(string ns, CancellationToken ct = default);
}
```

## Configuration Schema
```csharp
public class MemoryOptions
{
    public string VectorStoreType { get; set; } = "SqliteVss";
    public EmbeddingOptions Embedding { get; set; } = new();
}
```
