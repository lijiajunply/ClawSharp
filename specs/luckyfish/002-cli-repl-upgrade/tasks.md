# 任务列表：CLI 交互体验升级 (REPL 2.0)

**输入**: 来自 `/specs/luckyfish/002-cli-repl-upgrade/` 的设计文档。
**前提条件**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**组织方式**: 任务按用户故事分组，以便独立实施和测试。

## 格式: `[ID] [P?] [Story] 描述`

- **[P]**: 可并行运行（不同文件，无依赖关系）。
- **[Story]**: 该任务所属的用户故事（如 US1, US2, US3, US4）。
- 描述中包含确切的文件路径。

## 路径约定

- **核心库**: `ClawSharp.Lib/`
- **CLI 应用程序**: `ClawSharp.CLI/`
- **测试**: `ClawSharp.Lib.Tests/`

---

## 阶段 1：设置（共享基础设施）

**目的**: 项目初始化和依赖验证。

- [ ] T001 [P] 在 `ClawSharp.CLI/ClawSharp.CLI.csproj` 中验证 `Spectre.Console` 的项目依赖。

---

## 阶段 2：基础工作（阻塞性前提条件）

**目的**: 在实施任何用户故事之前必须完成的核心基础设施。

**⚠️ 关键**: 现有的 `ChatCommand.cs` 使用了庞大的 switch-case；需要进行重构以适应新命令的扩展。

- [ ] T002 重构 `ClawSharp.CLI/Commands/ChatCommand.cs` 中的命令处理逻辑，将单个命令（如 `/help`, `/new`）提取为独立的私有方法，以提高可维护性。

**检查点**: 基础就绪 - 命令注册现在具有可扩展性。

---

## 阶段 3：用户故事 1 - 增强的会话管理 (优先级: P1) 🎯 MVP

**目标**: 列出历史会话并支持通过编号进行切换。

**独立测试**: 使用 `/sessions` 列出，然后使用 `/sessions 1` 切换并查看历史回放。

### 用户故事 1 的测试 (根据项目宪法必填)

- [ ] T003 [US1] 在 `ClawSharp.Lib.Tests/RuntimeIntegrationTests.cs` 中增加集成测试，验证通过 `IThreadSpaceManager` 获取会话列表及通过 `IClawRuntime` 切换会话的逻辑。

### 用户故事 1 的实施

- [ ] T004 [US1] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中实现 `/sessions` 命令逻辑，从 `IClawKernel.ThreadSpaces.ListSessionsAsync` 获取会话。
- [ ] T005 [US1] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中使用 `Spectre.Console.Table` 创建格式化的会话列表输出。
- [ ] T006 [US1] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中实现 `/sessions <index>` 切换逻辑，更新活动会话。
- [ ] T007 [US1] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中实现成功切换会话后的自动历史回放（最近 5 条消息）。

**检查点**: 用户故事 1（会话管理）已完整实现并可通过测试验证。

---

## 阶段 4：用户故事 2 - 代码块语法高亮 (优先级: P1)

**目标**: 在 Agent 响应中渲染带语法高亮的 Markdown 代码块。

**独立测试**: 向 Agent 索要代码并验证终端输出是否带有颜色。

### 用户故事 2 的测试

- [ ] T008 [US2] 在 `ClawSharp.Lib.Tests/MarkdownParsingTests.cs` 中验证 Markdown 解析逻辑是否能正确识别不同语言的代码块。

### 用户故事 2 的实施

- [ ] T009 [US2] 更新 `ClawSharp.CLI/Commands/ChatCommand.cs` 中的流式输出循环，以跟踪响应是否包含 Markdown 代码块。
- [ ] T010 [US2] 在流式输出结束后，在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中使用 `Spectre.Console.Markdown` 重新渲染最终消息以应用语法高亮。

**检查点**: 用户故事 2（语法高亮）已完整实现。

---

## 阶段 5：用户故事 3 - 工具检查 (优先级: P2)

**目标**: 在会话期间查看可用工具及其权限状态。

**独立测试**: 输入 `/tools` 并验证表格是否正确显示已授权的工具。

### 用户故事 3 的实施

- [ ] T011 [US3] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中实现 `/tools` 命令逻辑，从运行时获取已授权的工具。
- [ ] T012 [US3] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中使用 `Spectre.Console.Table` 格式化并显示工具列表。

**检查点**: 用户故事 3（工具检查）已完整实现。

---

## 阶段 6：用户故事 4 - 多行输入支持 (优先级: P2)

**目标**: 通过终端粘贴模式或外部编辑器支持长提示词。

**独立测试**: 输入 `/paste`，输入多行，以 `.` 提交；测试使用外部编辑器的 `/edit`。

### 用户故事 4 的测试

- [ ] T013 [US4] 在 `ClawSharp.Lib.Tests/CliIntegrationTests.cs` 中模拟多行输入并验证其正确性。

### 用户故事 4 的实施

- [ ] T014 [US4] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中实现 `/paste` 模式，捕获输入直到输入仅包含 `.` 的行。
- [ ] T015 [US4] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中为 `/paste` 模式添加视觉反馈（例如 `Paste >` 提示符）。
- [ ] T016 [US4] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中实现 `/edit` 命令，使用 `System.Diagnostics.Process` 启动 `EDITOR` 环境变量定义的编辑器。

**检查点**: 用户故事 4（多行输入）已完整实现。

---

## 阶段 7：磨合与跨领域关注点

**目的**: 最终 UI 精化和性能验证。

- [ ] T017 [P] 在 `ClawSharp.CLI/Commands/ChatCommand.cs` 中更新 `/help` 文档，包含 REPL 2.0 命令。
- [ ] T018 [P] 在 `ClawSharp.CLI/Infrastructure/ReplPrompt.cs` 的建议列表中添加新命令。
- [ ] T019 **性能验证**: 测量会话切换时间，确保在大规模历史记录下仍满足 < 5s 的成功指标。
- [ ] T020 运行 `quickstart.md` 验证，确保所有记录的场景均按预期工作。

---

## 依赖关系与执行顺序

### 阶段依赖

- **设置 (阶段 1)**: 可立即开始。
- **基础工作 (阶段 2)**: 依赖于阶段 1 - 阻塞所有用户故事以避免代码重复。
- **用户故事 (阶段 3-6)**: 均依赖于阶段 2 的完成。US1 和 US2 优先级最高。
- **磨合 (阶段 7)**: 依赖于所有用户故事的完成。

### 并行机会

- T001 是一个独立检查。
- T017 和 T018 是独立的 UI 更新。
- 基础工作 (T002) 完成后，如果团队资源充足，US1 到 US4 可以并行开发。

---

## 实施策略

### MVP 优先 (用户故事 1 & 2)

1. 完成设置和基础重构。
2. 实现会话管理 (US1) - 核心生产力功能。
3. 实现语法高亮 (US2) - 核心视觉功能。
4. **停止并验证**。

### 增量交付

1. 添加工具检查 (US3)。
2. 添加多行输入 (US4)。
3. 最终 UI 磨合和自动提示更新。
