# Data Model: Persistent Vector Store and Automated RAG

## Entities

### VectorIndex
代表一个持久化的向量索引表。
- **TableName**: 映射到 SQLite 的虚拟表名称。
- **Dimension**: 向量维度（如 1536 对于 OpenAI）。
- **Metric**: 距离度量方式（如 L2, Cosine）。

### KnowledgeChunk
存储在向量库中的最小知识单元。
- **Id**: 唯一标识符 (GUID/String)。
- **Content**: 原始文本内容。
- **Metadata**: 关联的 JSON 元数据（如 Source, LineNumber）。
- **Embedding**: 浮点数向量 (存储为 BLOB 或二进制)。
- **Namespace**: 区分不同 Agent 或 Session 的命名空间（对应 FR-007）。

## Table Schema (SQLite-VSS)

```sql
-- 虚拟表存储向量
CREATE VIRTUAL TABLE vss_chunks USING vss0(
  embedding(1536) -- OpenAI 维度
);

-- 普通表存储元数据和文本
CREATE TABLE chunk_metadata (
  rowid INTEGER PRIMARY KEY,
  id TEXT NOT NULL,
  content TEXT NOT NULL,
  metadata TEXT,
  namespace TEXT NOT NULL
);
```

## State Transitions

1. **Ingestion**: Text -> Chunks -> Embeddings -> Save to SQLite.
2. **Query**: User Input -> Embedding -> VSS Search -> Retrieve Top-K Metadata.
3. **Injection**: Top-K Metadata -> Context -> Model Call.
