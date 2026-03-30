# 调研报告：CLI 交互体验升级 (REPL 2.0)

本报告总结了 REPL 2.0 升级的技术预研结果和决策。

## 1. /tools 命令实现

**目标**: 列出当前 Agent 及其关联的工具与权限。

**决策**:
- **数据来源**: 使用 `IClawRuntime.PrepareAgentAsync` 或直接从 `IClawKernel.Tools` 获取已授权工具。
- **展示方式**: 使用 `Spectre.Console.Table` 展示工具名称、描述和权限状态。
- **权限解析**: 依赖 `IPermissionResolver` 返回的 `ToolPermissionSet`。

## 2. /sessions 会话管理实现

**目标**: 列出历史会话并支持通过编号切换。

**决策**:
- **会话列表**: 调用 `IThreadSpaceManager.ListSessionsAsync(currentThreadSpaceId)` 获取当前空间下的所有会话。
- **会话展示**: 使用 `Spectre.Console.Table` 展示索引、SessionId (简略)、启动时间和最后一条消息预览。
- **会话切换**: 允许用户输入 `/sessions <index>`。如果提供了索引，则调用 `ISessionManager.GetAsync` 获取会话记录并更新当前 `sessionId`。
- **历史回放**: 切换后自动调用 `IClawRuntime.GetHistoryAsync` 并回放最后几条消息。

## 3. 代码块语法高亮实现

**目标**: 对 Agent 返回的代码块进行高亮处理。

**决策**:
- **组件选择**: 使用 `Spectre.Console.SyntaxHighlighter`。
- **流式输出挑战**: `Console.Write` 无法直接处理复杂的语法高亮逻辑。
- **方案**:
  - 在流式输出过程中，检测 Markdown 代码块的开始 (` ``` `)。
  - 进入代码块模式后，开始缓冲内容。
  - 对于流式过程中的实时反馈，可以先以原样输出。
  - **最终优化**: 在 Turn 结束后，重新渲染包含代码块的完整消息，使用 `Spectre.Console.Markdown` 或 `SyntaxHighlighter` 替换原始输出。

## 4. 多行输入支持 (/paste 和 /edit)

### /paste 模式
**目标**: 终端内直接输入多行文本。

**决策**:
- **交互流程**: 
  1. 用户输入 `/paste`。
  2. 进入特殊循环，不再即时发送。
  3. 支持持续输入，直到输入结束序列（如空行加 `.` 或 `Ctrl+D`）。
  4. 提示符更改为 `Paste >` 以作区分。

### /edit 命令
**目标**: 调用系统编辑器。

**决策**:
- **实现方式**:
  1. 创建临时文件 (`.md`)。
  2. 使用 `Process.Start` 启动由 `EDITOR` 环境变量（或 Windows 上的 `notepad`）指定的程序。
  3. 使用 `Task.Run` 等待进程结束。
  4. 读取文件内容并将其作为用户消息发送。

## 5. REPL 代码结构优化

**决策**:
- 将 `ChatCommand.cs` 中的命令处理逻辑拆分为更小的处理器或使用策略模式，以保持代码可维护性。
- 更新 `ReplPrompt` 以更好地支持不同类型的提示模式。
