# Tasks: Persistent Vector Store and Automated RAG

**Input**: Design documents from `/specs/luckyfish/002-persistent-vector-rag/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 项目初始化与依赖配置

- [x] T001 安装 NuGet 依赖：`FastEmbed.Net`, `Microsoft.Data.Sqlite` 到 `ClawSharp.Lib/ClawSharp.Lib.csproj`
- [x] T002 [P] 在 `ClawSharp.Lib/Configuration/ClawOptions.cs` 中添加 `MemoryOptions` 配置架构
- [x] T003 [P] 更新 `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs` 以支持新内存组件的 DI 注册

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 定义核心接口和抽象，为各用户故事提供基础

- [x] T004 定义 `IEmbeddingProvider` 接口于 `ClawSharp.Lib/Memory/IEmbeddingProvider.cs`
- [x] T005 定义 `KnowledgeChunk` 实体于 `ClawSharp.Lib/Memory/MemoryContracts.cs`
- [x] T006 完善 `IVectorStore` 接口 definition于 `ClawSharp.Lib/Memory/IVectorStore.cs`

---

## Phase 3: User Story 1 - 跨会话记忆持久化 (Priority: P1) 🎯 MVP

**Goal**: 实现基于 SQLite-VSS 的持久化向量存储，确保重启后记忆不丢失。

**Independent Test**: 存入向量数据，重启程序，通过命名空间检索确认数据存在。

- [x] T007 [US1] 实现 `SqliteVssVectorStore` 类于 `ClawSharp.Lib/Memory/SqliteVssVectorStore.cs`
- [x] T008 [US1] 实现 SQLite-VSS 扩展自动加载及服务启动时的连接状态恢复逻辑 (FR-005)
- [x] T009 [US1] 实现 `vss_chunks` 虚拟表和 `chunk_metadata` 元数据表的初始化逻辑
- [x] T010 [US1] 实现 `UpsertAsync` 和 `SearchAsync` 的 SQL 向量检索逻辑
- [x] T011 [US1] 编写集成测试 `ClawSharp.Lib.Tests/MemoryTests.cs` 验证持久化与检索

---

## Phase 4: User Story 2 - 高质量语义检索 (Priority: P2)

**Goal**: 实现真实 Embedding 提供者（OpenAI 和本地模型），提升检索准确度。

**Independent Test**: 使用同义词进行检索测试，确认召回结果优于简单关键词匹配。

- [x] T012 [P] [US2] 实现 `OpenAiEmbeddingProvider` 于 `ClawSharp.Lib/Memory/OpenAiEmbeddingProvider.cs`
- [x] T013 [P] [US2] 实现 `LocalEmbeddingProvider` (基于 FastEmbed.Net) 于 `ClawSharp.Lib/Memory/LocalEmbeddingProvider.cs`
- [x] T014 [US2] 在 `LocalEmbeddingProvider` 中处理模型下载与 ONNX 运行时初始化
- [x] T015 [US2] 编写测试 `ClawSharp.Lib.Tests/EmbeddingTests.cs` 验证不同 Provider 的 Embedding 生成

---

## Phase 5: User Story 3 - 自动化 RAG 注入 (Priority: P3)

**Goal**: 在 `ClawRuntime` 中自动拦截对话并注入检索到的知识片段。

**Independent Test**: 模拟对话流，拦截发送给 LLM 的 Context，确认包含相关的知识块。

- [x] T016 [US3] 修改 `ClawSharp.Lib/Runtime/ClawRuntime.cs` 中的 `RunTurnStreamingAsync` 逻辑
- [x] T017 [US3] 实现自动化检索逻辑：根据用户输入从 `IVectorStore` 提取 Context
- [x] T018 [US3] 实现 Context 注入逻辑：将 Top-K 结果转化为 SystemMessage 或 UserMessage 前缀
- [x] T019 [US3] 编写集成测试 `ClawSharp.Lib.Tests/RuntimeIntegrationTests.cs` 验证端到端 RAG 流程

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 完善错误处理、性能优化与文档验证。

- [x] T020 优化 `LocalEmbeddingProvider` 的资源释放逻辑 (IDisposable)
- [x] T021 [P] 在 `ClawSharp.CLI` 中添加对 RAG 状态的显示支持
- [x] T022 [P] 创建/导入标准语义检索基准测试集并编写准确率验证脚本，确认 SC-003 目标（提升 50%）
- [x] T023 [P] 编写端到端 RAG 延迟性能测试脚本并记录结果，验证 SC-002 目标（< 500ms）
- [x] T024 运行 `quickstart.md` 中定义的示例，验证全流程通畅
- [x] T025 更新 `README.md` 说明持久化存储的使用方法

---

## Dependencies & Execution Order

1. **Phase 1 & 2** 是所有工作的基石。
2. **Phase 3 (US1)** 必须先于 **Phase 5 (US3)**，因为自动化 RAG 需要持久化存储支撑。
3. **Phase 4 (US2)** 可与 Phase 3 并行开发。
4. **Phase 5 (US3)** 依赖于 US1 和 US2 的完成。

## Parallel Opportunities

- T002, T003 可以并行执行。
- T012, T013 可以在 US2 阶段并行执行。
- Phase 3 (持久化) 和 Phase 4 (Embedding) 的模型/Provider 部分可以由两个开发者并行完成。
