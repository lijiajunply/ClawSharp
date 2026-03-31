# Feature Specification: Persistent Vector Store and Automated RAG

**Feature Branch**: `luckyfish/002-persistent-vector-rag`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "* 实现持久化向量存储: 替换 InMemoryVectorStore。建议引入 SQLite-VSS 扩展或 DuckDB，确保 Agent 的记忆在重启后依然存在。 * 引入真实 Embedding: 对接 OpenAI、HuggingFace 或本地 ONNX 模型。目前的 SimpleEmbeddingProvider 无法处理真实的语义检索。 * 自动化 RAG 注入: 优化 ClawRuntime.RunTurnStreamingAsync，在每一轮对话开始前，自动根据用户输入检索关联的 Workspace/Agent 记忆，并将其注入到 Context 中。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 跨会话记忆持久化 (Priority: P1)

用户与 Agent 进行了一次深入对话，讨论了复杂的项目需求。随后用户关闭了应用程序（或系统重启）。当用户再次打开程序并继续对话时，Agent 能够根据之前的记忆提供连贯的回复，而不需要用户重新解释背景。

**Why this priority**: 这是该功能的核心价值。目前的 InMemoryVectorStore 会在重启后丢失所有上下文，导致 Agent 失去“长期记忆”。

**Independent Test**: 可以通过以下方式独立测试：在 Session A 中存入特定事实，重启程序，在 Session B 中询问该事实，Agent 能够准确检索。

**Acceptance Scenarios**:

1. **Given** 启用了持久化存储的 Agent, **When** 用户存入一条信息并关闭程序, **Then** 重启后该信息依然可以通过向量检索找到。
2. **Given** 多个不同的 Session, **When** 系统重启, **Then** 每个 Session 对应的本地向量索引都能正确加载。

---

### User Story 2 - 高质量语义检索 (Priority: P2)

用户使用自然语言进行提问（例如“关于性能优化的建议”），系统能够通过 Embedding 模型理解查询的深层含义，即使文档中没有出现“性能优化”字样（而是“吞吐量提升”或“延迟降低”），也能精准找到相关片段。

**Why this priority**: 目前的 SimpleEmbeddingProvider 仅能进行简单的模式匹配，无法实现真正的 RAG 价值。

**Independent Test**: 使用同义词或相关概念进行查询，验证返回结果的相关度高于简单的关键词匹配。

**Acceptance Scenarios**:

1. **Given** 接入了真实 Embedding 提供者（如 OpenAI）, **When** 用户输入查询, **Then** 系统返回语义相关的文档片段而非仅限字面匹配。
2. **Given** 本地 ONNX 模型配置, **When** 在离线状态下运行, **Then** 系统仍能生成 Embedding 并完成语义搜索。

---

### User Story 3 - 自动化 RAG 注入 (Priority: P3)

开发者无需手动编写繁琐的检索逻辑。在每一轮对话开始前，ClawRuntime 自动分析用户意图，从关联的存储中提取背景知识，并在发送给大模型前将其注入到 Context 中，使回复更具参考价值。

**Why this priority**: 简化开发者的工作，使 RAG 成为系统的内置“黑盒”能力，提升整体智能化水平。

**Independent Test**: 监控 `RunTurnStreamingAsync` 的输入 Context，确认在用户输入之前已自动包含了检索到的知识片段。

**Acceptance Scenarios**:

1. **Given** 配置了 Workspace 记忆, **When** 用户发送消息, **Then** `ClawRuntime` 在调用模型前自动注入了相关的上下文。

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系统必须支持向量数据的持久化存储，默认支持 SQLite-VSS 后端，并保留扩展接口以支持未来集成其他存储引擎（如 DuckDB）。
- **FR-002**: 系统必须提供可插入的 Embedding 接口，并同时支持云端（OpenAI）和本地（基于 ONNX/FastEmbed）的配置方案，用户可根据网络环境灵活切换。
- **FR-003**: `ClawRuntime` 必须在每轮对话开始前，自动触发基于当前输入的语义检索。
- **FR-004**: 检索到的 Top-K 相关片段必须以结构化的方式注入到当前对话的 Context 中。
- **FR-005**: 系统必须支持在重启后自动重新连接并加载现有的向量索引。
- **FR-007**: 自动化 RAG 检索默认仅限当前 Agent 或 Session 的私有记忆命名空间，以确保隐私并减少干扰。

### Key Entities *(include if feature involves data)*

- **VectorStore**: 代表持久化的向量数据库，负责存储 Embedding 和对应的 Metadata。
- **EmbeddingProvider**: 代表 Embedding 生成器，将文本转化为高维向量。
- **KnowledgeChunk**: 被切分并索引的知识片段。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 应用程序重启后，向量检索的召回率保持 100%（相对于重启前）。
- **SC-002**: 自动化 RAG 注入过程引入的端到端延迟增加不超过 500ms（在本地 Embedding 模式下）。
- **SC-003**: 在标准语义测试集上，新 Embedding 方案的检索准确率相比旧版提升 50% 以上。
- **SC-004**: 实现 0 代码入侵的 RAG 注入，即开发者只需开启开关，无需修改业务代码。

## Assumptions

- 假定用户环境支持加载 SQLite 扩展或 DuckDB 库。
- 假定系统已有基础的 `IClawRuntime` 和 `IVectorStore` 接口定义可供扩展。
- 假定 Embedding 提供者（如 OpenAI）的 API 访问是稳定的，或用户已下载本地模型。
- 假定文本切分（Chunking）策略在现有框架中已有默认实现。
