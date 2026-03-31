# Research: Persistent Vector Store and Automated RAG

## Decision: SQLite-VSS vs DuckDB

- **Decision**: 使用 **SQLite-VSS** 作为持久化向量存储。
- **Rationale**: 
    - 现有系统已使用 SQLite 存储 Session 和 History，引入 VSS 扩展可以保持存储引擎的一致性。
    - SQLite-VSS 提供了极佳的 C/C++ 互操作性，且在 .NET 中通过 `Microsoft.Data.Sqlite` 加载扩展非常成熟。
- **Alternatives considered**: 
    - **DuckDB**: 虽有优秀的向量计算能力，但作为主存储引擎与现有 EF Core 的集成相对复杂。
    - **Lucene.Net**: 文本搜索强大，但向量搜索并非原生优势，且较重。

## Decision: Local Embedding Library

- **Decision**: 使用 **FastEmbed.Net** (基于 ONNX Runtime)。
- **Rationale**: 
    - FastEmbed 是行业公认的高性能本地 Embedding 生成库。
    - 它封装了常用的轻量级模型（如 BGE-small-zh），适合本地运行且内存占用小。
- **Alternatives considered**: 
    - **Direct ONNX Runtime**: 手动加载模型和处理 Tokenizer 太过繁琐。
    - **OpenAI API only**: 违反 Local-first 指令，无法在离线环境下工作。

## Decision: RAG Injection Hook

- **Decision**: 在 `ClawRuntime.RunTurnStreamingAsync` 的 `Context` 构建阶段注入。
- **Rationale**: 
    - 每一轮对话的核心入口，可以在消息发送给大模型前拦截并注入上下文。
    - 注入的数据应当作为一组特殊的 `SystemMessage` 或附带在 `UserMessage` 的前缀中。
- **Alternatives considered**: 
    - **Model Provider 装饰器**: 粒度太细，难以访问到具体的 Workspace/Agent 记忆上下文。
    - **Agent 级别**: 需要在每个 Agent 定义中手动配置，不够“自动化”。

## Integration Pattern: SQLite-VSS in .NET

1. 需要通过 `connection.LoadExtension("sqlite_vss")` 加载库。
2. 数据表使用 `VIRTUAL TABLE ... USING vss0`。
3. 向量存储采用 `BLOB` 格式或 VSS 特有的列类型。

## Dependency Best Practices

- `Microsoft.Data.Sqlite`: 支持扩展加载。
- `FastEmbed.Net`: 需确保运行时二进制库在 Build 时正确复制。
