# REPL 斜杠命令契约

**Branch**: `luckyfish/002-redesign-threadspace-cli` | **Date**: 2026-03-30

---

## 概述

ClawSharp CLI REPL 支持以下斜杠命令。所有斜杠命令均以 `/` 开头，**不会被发送给 Agent**。输入未知斜杠命令时，显示友好提示并建议 `/help`。

---

## 命令列表

### `/help`

**触发**: 用户输入 `/help`
**行为**: 在当前 REPL 中显示格式化命令帮助表格，不退出对话，不创建新会话。
**输出格式**:
```
╔═══════════════════════════════════════╗
║  Available Commands                   ║
╠═════════════╤═════════════════════════╣
║ /help        │ Show this help          ║
║ /new         │ Start a new session     ║
║ /resume      │ Resume last session     ║
║ /cd <path>   │ Switch to directory TS  ║
║ /home        │ Switch to global TS     ║
║ /clear       │ Clear the screen        ║
║ /quit, /exit │ Exit the REPL           ║
╚═════════════╧═════════════════════════╝
```

---

### `/new`

**触发**: 用户输入 `/new`
**行为**:
1. 在当前 ThreadSpace 下创建新 Session（调用 `runtime.StartSessionAsync`）
2. 将 REPL 内部 `currentSessionId` 更新为新 session
3. 清空当前屏幕显示的对话历史（`AnsiConsole.Clear()`）
4. 显示确认消息：`[green]New session started.[/]`

**副作用**: 旧 session 的历史保留在数据库中，可通过 `/resume` 恢复（加载最近一次，即旧 session）。

---

### `/resume`

**触发**: 用户输入 `/resume`
**行为**:
1. 通过 `kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpaceId)` 获取当前 ThreadSpace 下的 session 列表（倒序）
2. 取最近一次 **不等于当前 session** 的 session（即上一个 session）
3. 若存在：更新 `currentSessionId`，加载并显示其历史消息摘要
4. 若不存在（仅一个 session）：显示 `[yellow]No previous session found.[/]`

**历史摘要显示**: 仅显示最后 5 条消息的角色和摘要（前 80 字符），不重新打印全部历史。

---

### `/cd <path>`

**触发**: 用户输入 `/cd /path/to/project` 或 `/cd relative/path`
**参数**: `<path>` — 目录路径，支持绝对路径和相对路径（相对于 `ClawOptions.Runtime.WorkspaceRoot`）

**行为**:
1. 将路径解析为绝对路径（`Path.GetFullPath`）
2. 验证路径存在；不存在则显示错误 `[red]Path not found: {path}[/]`，不切换
3. 调用 `kernel.ThreadSpaces.GetByBoundFolderPathAsync(absolutePath)`
   - 存在：复用该 ThreadSpace
   - 不存在：调用 `kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(dirName, absolutePath))` 创建新 ThreadSpace
4. 加载该 ThreadSpace 的最近 Session（若存在），否则创建新 Session
5. 更新 REPL 状态：`currentThreadSpaceId`、`currentSessionId`、`currentThreadSpaceName`、`isGlobal = false`
6. 更新提示符为 `[project-name] > `

**错误处理**:
- 路径不存在 → `[red]Directory not found: {path}[/]`
- 路径为文件而非目录 → `[red]Path is not a directory: {path}[/]`

---

### `/home`

**触发**: 用户输入 `/home` 或 `/cd`（无参数）
**行为**:
1. 获取全局 ThreadSpace（`kernel.ThreadSpaces.GetInitAsync()`）
2. 加载全局 ThreadSpace 的最近 Session，或创建新 Session
3. 更新 REPL 状态：切回全局 ThreadSpace
4. 更新提示符为 `[global] > `

---

### `/clear`

**触发**: 用户输入 `/clear`
**行为**: 清空终端屏幕（`AnsiConsole.Clear()`），不影响 session 状态。

---

### `/quit` / `/exit`

**触发**: 用户输入 `/quit` 或 `/exit`，或按 Ctrl+C
**行为**: 干净退出 REPL，返回 exit code 0。不丢失消息历史（历史已持久化到数据库）。

---

### 未知命令

**触发**: 用户输入任何以 `/` 开头但不在已知命令列表中的字符串
**行为**: 显示 `[yellow]Unknown command: {cmd}. Type /help to see available commands.[/]`，**不将其发送给 Agent**，继续等待用户输入。

---

## 欢迎头部格式

REPL 启动时显示：

```
╔══════════════════════════════════════════════════╗
║  ClawSharp v1.0.0                                ║
║  Agent: {agentId}   ThreadSpace: {threadSpaceName} ║
║  Type /help for commands                         ║
╚══════════════════════════════════════════════════╝
```

若存在上次会话：额外显示 `[grey]Tip: type /resume to continue last conversation.[/]`

---

## 提示符格式

| 状态 | 提示符 |
|------|--------|
| 全局 ThreadSpace | `[global] > ` |
| 目录绑定 ThreadSpace | `[project-name] > ` |

实现：`ThemeConfig.GetUserPrefix(currentThreadSpaceName)` 或等效逻辑。
