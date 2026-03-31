# Tasks: SpecKit 脚手架集成与自举开发

**Input**: Design documents from `/specs/luckyfish/002-speckit-scaffolder-integration/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行执行 (不同文件，无依赖)
- **[Story]**: 该任务所属的用户故事 (例如 US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 项目基础结构初始化与工具配置

- [ ] T001 [P] 创建功能所需的目录结构 (specs/luckyfish/002-speckit-scaffolder-integration/contracts/)
- [ ] T002 在 ClawSharp.Lib 中添加 YamlDotNet 依赖以便解析元数据
- [ ] T003 [P] 配置测试环境，准备 xUnit 集成测试基础类

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 核心基础设施，必须在任何用户故事实施前完成

- [ ] T004 实现 `MarkdownSectionParser` 基础工具类，用于解析 Markdown 区块
- [ ] T005 [P] 定义 `SpecKitDefinition` 和 `FeatureMetadata` 数据模型类
- [ ] T006 在 `ClawOptions` 中增加 SpecKit 的配置路径
- [ ] T007 实现 `FeatureContextRepository` 以便在 SQLite 中记录功能开发进度

**Checkpoint**: 基础架构就绪 - 用户故事可以开始并行实施

---

## Phase 3: User Story 1 - 带有 SpecKit 的完整项目初始化 (Priority: P1) 🎯 MVP

**Goal**: 使 `/init-proj` 命令能够自动注入 SpecKit 治理框架

**Independent Test**: 运行 `/init-proj` 后验证 `.specify` 目录及其模板是否存在

### Implementation for User Story 1

- [ ] T008 定义 `ISpecKitProvider` 接口及其在 `ClawSharp.Lib/Projects/SpecKitProvider.cs` 中的实现
- [ ] T009 [P] [US1] 编写 SpecKit 资源嵌入逻辑（从 DLL 资源或固定目录读取模板）
- [ ] T010 增强 `ClawSharp.Lib/Projects/IProjectScaffolder.cs` 接口，添加 `ApplySpecKitAsync` 方法
- [ ] T011 [US1] 在 `ProjectScaffolder.cs` 的 `CreateProjectAsync` 流程末尾集成 SpecKit 注入
- [ ] T012 [US1] 编写集成测试 `ClawSharp.Lib.Tests/ProjectScaffoldingTests.cs` 验证 SpecKit 注入逻辑

**Checkpoint**: 用户故事 1 已完成，新创建的项目现在自动具备 SpecKit 治理能力

---

## Phase 4: User Story 2 - 从计划自动生成功能脚手架 (Priority: P1)

**Goal**: 实现从 `plan.md` 到物理文件结构（分支、文件、任务）的自动转化

**Independent Test**: 提供 `plan.md`，验证系统是否创建了正确的 Git 分支和占位文件

### Implementation for User Story 2

- [ ] T013 [P] [US2] 实现 `IScaffoldAnalyzer` 接口，用于对比 `plan.md` 与磁盘状态
- [ ] T014 [US2] 编写 `ScaffoldAnalyzer` 逻辑，解析 `### Source Code` 区块提取文件清单
- [ ] T015 [P] [US2] 实现 `IPlannerAgent` 及其在 `ClawSharp.Lib/Agents/PlannerAgent.cs` 中的自举逻辑
- [ ] T016 [US2] 集成 Git 操作工具类，支持自动创建分支 `luckyfish/ID-ShortName`
- [ ] T017 [US2] 实现占位文件生成逻辑，根据计划中的文件类型生成 C# 类或 Markdown
- [ ] T018 [US2] 实现 `tasks.md` 自动生成逻辑，从 `plan.md` 提取里程碑

**Checkpoint**: 用户故事 2 已完成，Planner Agent 现在可以驱动物理脚手架的生成

---

## Phase 5: User Story 3 - 集成的 SpecKit CLI 命令 (Priority: P2)

**Goal**: 在 CLI 中提供统一的 `/speckit` 入口并实现交互式确认

**Independent Test**: 执行 `/speckit scaffold` 并通过交互界面确认生成动作

### Implementation for User Story 3

- [ ] T019 [P] [US3] 创建 `ClawSharp.CLI/Commands/SpecKitCommands.cs` 并注册 `/speckit` 根命令
- [ ] T020 [US3] 实现 `/speckit init` 子命令，调用现有的 `create-new-feature.sh` 逻辑
- [ ] T021 [US3] 实现 `/speckit scaffold` 子命令，与 `IPlannerAgent` 交互
- [ ] T022 [US3] 使用 `Spectre.Console` 实现交互式确认界面（显示建议的变更列表并等待确认）
- [ ] T023 [US3] 集成文件监听钩子，当 `plan.md` 保存时在 CLI 中主动触发交互提示

**Checkpoint**: 所有用户故事均已完成，SpecKit 工作流已完全集成到 CLI

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T024 更新 `quickstart.md` 验证自举流程的完整性
- [ ] T025 [P] 编写最终集成测试，模拟从初始化到功能脚手架生成的完整生命周期
- [ ] T026 优化 `MarkdownSectionParser` 的异常处理和边缘情况处理

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖。
- **Foundational (Phase 2)**: 依赖 Setup 完成。
- **User Stories (Phase 3+)**: 依赖 Foundational 完成。US1 和 US2 逻辑上独立，可以并行或按优先级顺序执行。
- **Polish (Final Phase)**: 依赖所有用户故事完成。

### Parallel Opportunities

- T001, T003 可并行。
- T005, T006 可并行。
- T008, T009 可并行。
- T013, T015 可并行。
- T019 可与其他 UI 任务并行。

---

## Implementation Strategy

### MVP 路线 (User Story 1 & 2)

1. 完成基础架构（Phase 1 & 2）。
2. 实现 `ISpecKitProvider`（US1）确保新项目可用。
3. 实现 `IPlannerAgent`（US2）的核心自举能力，通过命令触发。
4. **验证点**：能够通过 CLI 命令根据 `plan.md` 自动创建分支和文件。

### 增量交付

1. 基础 + US1 -> 具备规范感知脚手架。
2. 增加 US2 -> 具备自动自举能力。
3. 增加 US3 -> 完整的 CLI 交互体验。
 US2 -> 具备自动自举能力。
3. 增加 US3 -> 完整的 CLI 交互体验。
