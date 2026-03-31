# Tasks: DuckDB 分析层落地与 `claw stats` 命令

**Input**: Design documents from `/specs/luckyfish/002-claw-stats-duckdb/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 准备开发环境与基础结构

- [ ] T001 确认 `ClawSharp.Lib/Runtime/AnalyticsContracts.cs` 中的现有接口定义
- [ ] T002 确认 `ClawSharp.CLI` 项目中 `Spectre.Console` 的依赖状态

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 核心分析接口与实体定义（阻塞所有后续任务）

- [ ] T003 [P] 在 `ClawSharp.Lib/Runtime/AnalyticsContracts.cs` 中定义 `TokenUsageMetric` 记录
- [ ] T004 [P] 在 `ClawSharp.Lib/Runtime/AnalyticsContracts.cs` 中定义 `ToolUsageMetric` 记录
- [ ] T005 [P] 在 `ClawSharp.Lib/Runtime/AnalyticsContracts.cs` 中定义 `AgentPerformanceMetric` 记录
- [ ] T006 扩展 `ClawSharp.Lib/Runtime/AnalyticsContracts.cs` 中的 `ISessionAnalyticsService` 接口，增加趋势查询、工具统计和 Agent 性能查询方法

---

## Phase 3: User Story 1 - 资源消耗概览 (Priority: P1) 🎯 MVP

**Goal**: 实现 `claw stats` 基础命令，展示 Token 消耗汇总

**Independent Test**: 运行 `claw stats` 应能显示当前 ThreadSpace 的 Token 消耗表格

- [ ] T007 [US1] 在 `ClawSharp.Lib/Runtime/AnalyticsServices.cs` 中实现 `GetTokenUsageTrendAsync` (基于 `TurnCompleted` 事件 payload 解析)
- [ ] T008 [US1] 在 `ClawSharp.CLI/Commands/StatsCommands.cs` 中创建 `ClawStatsCommand` 基础框架
- [ ] T009 [US1] 在 `ClawSharp.CLI/Infrastructure/StatsRenderer.cs` 中实现基础统计摘要的表格渲染逻辑 (适配 SC-003：80列终端标准)
- [ ] T010 [US1] 集成 `ClawStatsCommand` 到 CLI 命令行解析器
- [ ] T011 [US1] 编写集成测试 `ClawSharp.Lib.Tests/AnalyticsTests.cs` 验证 Token 统计准确性

**Checkpoint**: 此时 `claw stats` 基础功能应可独立工作并正确展示 Token 数据

---

## Phase 4: User Story 2 - 工具调用排行 (Priority: P2)

**Goal**: 实现 `--tools` 选项，展示工具使用频率与成功率

**Independent Test**: 运行 `claw stats --tools` 应列出所有已调用工具及其成功/失败次数

- [ ] T012 [US2] 在 `ClawSharp.Lib/Runtime/AnalyticsServices.cs` 中实现 `GetToolUsageStatsAsync` (基于 `message_blocks` 和 `ToolCallCompleted` 事件解析)
- [ ] T013 [US2] 在 `ClawSharp.CLI/Commands/StatsCommands.cs` 中增加 `--tools` 标志位解析逻辑
- [ ] T014 [US2] 在 `ClawSharp.CLI/Infrastructure/StatsRenderer.cs` 中实现工具统计的渲染逻辑 (BarChart 或 Table，适配 SC-003)
- [ ] T015 [US2] 更新 `ClawSharp.Lib.Tests/AnalyticsTests.cs` 增加工具统计的验证案例

**Checkpoint**: 用户故事 1 和 2 此时均应能独立运行

---

## Phase 5: User Story 3 - Agent 性能趋势 (Priority: P3)

**Goal**: 实现 `--agents` 选项，监控响应时长分布

**Independent Test**: 运行 `claw stats --agents` 应显示各 Agent 的平均响应延迟

- [ ] T016 [US3] 在 `ClawSharp.Lib/Runtime/AnalyticsServices.cs` 中实现 `GetAgentPerformanceAsync` (解析 `TurnCompleted` 中的响应耗时)
- [ ] T017 [US3] 在 `ClawSharp.CLI/Commands/StatsCommands.cs` 中增加 `--agents` 标志位解析逻辑
- [ ] T018 [US3] 在 `ClawSharp.CLI/Infrastructure/StatsRenderer.cs` 中实现性能指标的渲染逻辑 (适配 SC-003)
- [ ] T019 [US3] 更新 `ClawSharp.Lib.Tests/AnalyticsTests.cs` 增加性能指标的验证案例

**Checkpoint**: 所有三个分析维度均已完成并可独立工作

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 细节完善与文档更新

- [ ] T020 [P] 在 `ClawSharp.CLI/Infrastructure/StatsRenderer.cs` 中处理“无数据”状态的友好提示
- [ ] T021 [P] 完善 `claw stats --period` 参数的支持，确保时间范围过滤逻辑正确
- [ ] T022 [P] 运行 `quickstart.md` 中的所有验证场景确保功能符合预期
- [ ] T023 [P] 文档更新：在 README 或 Help 中增加 `claw stats` 的详细说明
- [ ] T024 [P] 执行并发性能测试：启动活跃 AI 会话时并行运行 `claw stats`，验证 SC-001 (结果展示 < 2s) 且会话无停顿 (对应 FR-007)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 立即开始。
- **Foundational (Phase 2)**: 依赖 Phase 1，**阻塞** 所有用户故事。
- **User Stories (Phase 3+)**: 均依赖 Phase 2。建议按顺序实现 (P1 -> P2 -> P3)，虽然它们在代码层面相对独立。

### User Story Dependencies

- **US1 (P1)**: 核心路径，应首先完成。
- **US2 (P2)**: 独立于 US1，但依赖基础分析服务。
- **US3 (P3)**: 独立于 US1/US2。

### Parallel Opportunities

- T003, T004, T005 (实体定义) 可并行。
- T020, T021, T023, T024 (Polish) 可并行。

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1 & 2。
2. 完成 Phase 3 (US1)。
3. **验证**: 运行 `claw stats` 看到 Token 数据。

### Incremental Delivery

1. 基础 Token 统计落地。
2. 迭代加入工具分析。
3. 迭代加入性能指标。
4. 最后统一润色 UI。
