# Feature Specification: ThreadSpace 重新设计与 CLI 体验优化

**Feature Branch**: `luckyfish/002-redesign-threadspace-cli`
**Created**: 2026-03-30
**Status**: Draft
**Input**: User description: "我现在想重新设计 线程空间(ThreadSpace) .默认会有一个全局的线程空间,并没有任何的工作区; 且现在我想让 CLI 的设计更贴近于 Gemini/Claude"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 直接启动即可对话 (Priority: P1)

用户只需运行 `claw`（不带任何子命令），即可直接进入对话 REPL，无需预先创建工作区或初始化任何配置。系统自动使用全局默认 ThreadSpace 承载会话。这是最常见的日常使用场景。

**Why this priority**: 消除了"先初始化工作区才能对话"的摩擦，与 Gemini CLI / Claude Code 的零配置启动体验对齐，是本次改版的核心价值。

**Independent Test**: 在一个从未使用过 ClawSharp 的空目录中运行 `claw`，不执行任何额外命令，直接输入一条消息，应当收到 Agent 回复。

**Acceptance Scenarios**:

1. **Given** 系统已安装且无任何已有配置，**When** 用户执行 `claw`，**Then** 终端显示欢迎头部（版本号、活跃 Agent 名称）并出现输入提示符，等待用户输入。
2. **Given** 用户处于全局 ThreadSpace 中，**When** 用户输入一条普通消息并回车，**Then** Agent 以流式方式输出回复，渲染 Markdown 格式。
3. **Given** 用户处于对话中，**When** 用户输入 `/quit` 或按 Ctrl+C，**Then** 干净退出 REPL，不丢失已有消息历史。

---

### User Story 2 - 全局 ThreadSpace 的会话延续 (Priority: P1)

用户再次启动 CLI 时，可以选择继续上一次的全局对话，而不是每次都开一个新会话。体验类似于 Gemini 的 "Continue last conversation" 或 Claude Code 的会话恢复。

**Why this priority**: 对话连续性是 AI 助手体验的基础。用户不应每次重启都从头开始。

**Independent Test**: 启动 CLI，发送一条消息后退出；再次启动，选择继续上次会话，发送引用上次内容的消息，Agent 能正确理解上下文。

**Acceptance Scenarios**:

1. **Given** 全局 ThreadSpace 中存在已有会话，**When** 用户执行 `claw`，**Then** 提示符或欢迎信息中提示可用 `/resume` 继续上次对话。
2. **Given** 用户输入 `/resume`，**When** 执行，**Then** 加载最近一次会话的消息历史并继续对话。
3. **Given** 用户希望开启新对话，**When** 用户输入 `/new`，**Then** 在同一 ThreadSpace 下创建新会话，历史清空。

---

### User Story 3 - 切换到目录绑定的 ThreadSpace (Priority: P2)

用户在对话中需要针对特定项目工作时，可以通过命令将当前 ThreadSpace 切换到绑定了某个目录的 ThreadSpace。切换后，Agent 的工具调用将以该目录为根，提示符也随之更新以反映当前上下文。

**Why this priority**: 维持项目级工作流能力，同时让全局模式成为默认，不强迫用户先绑定目录。

**Independent Test**: 在全局 ThreadSpace 中输入 `/cd /path/to/project`，成功切换到该项目的 ThreadSpace（若不存在则自动创建），提示符更新为项目名称。

**Acceptance Scenarios**:

1. **Given** 用户处于全局 ThreadSpace，**When** 用户输入 `/cd /path/to/project`，**Then** 系统为该路径创建或复用 ThreadSpace，并切换当前会话到新 ThreadSpace，提示符更新为项目名。
2. **Given** 用户处于目录绑定的 ThreadSpace，**When** 用户输入 `/cd` 或 `/home`，**Then** 切换回全局 ThreadSpace。
3. **Given** 指定路径不存在，**When** 用户输入 `/cd /nonexistent/path`，**Then** 显示明确的错误提示，不切换 ThreadSpace。

---

### User Story 4 - CLI 内联帮助与命令发现 (Priority: P3)

用户在 REPL 中可以输入 `/help` 查看所有可用的斜杠命令及其简短说明，无需退出查阅文档。

**Why this priority**: 提升可发现性，减少用户学习成本，是 Gemini/Claude CLI 标配体验。

**Independent Test**: 在 REPL 中输入 `/help`，终端显示格式化的命令列表，包含 `/new`、`/resume`、`/cd`、`/clear`、`/help`、`/quit` 等所有可用命令及说明。

**Acceptance Scenarios**:

1. **Given** 用户在 REPL 中，**When** 输入 `/help`，**Then** 显示结构化的命令帮助表格，不退出对话。
2. **Given** 用户输入未知命令如 `/foo`，**When** 执行，**Then** 提示该命令不存在并建议输入 `/help` 查看可用命令。

---

### Edge Cases

- 全局 ThreadSpace 首次初始化失败（如数据库权限问题）时，显示明确错误并退出，不进入对话状态。
- 多个 Agent 可用且无默认配置时，自动选择注册表中第一个 Agent，并在欢迎头部明确告知用户当前使用的 Agent。
- `/cd` 切换到一个已有多个 Session 的 ThreadSpace 时，加载最近一次活跃会话（与 `/resume` 行为一致）。
- 用户在没有任何 Agent 注册的情况下启动 REPL，系统给出明确提示而非崩溃。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系统 MUST 在用户直接执行 `claw` 时进入交互式 REPL 模式，无需任何子命令或参数。
- **FR-002**: 系统 MUST 在启动时自动确保全局默认 ThreadSpace 存在，该 ThreadSpace 不绑定任何目录。
- **FR-003**: `ThreadSpaceRecord` 的 `BoundFolderPath` MUST 变为可选字段，全局 ThreadSpace 可不携带目录绑定。
- **FR-004**: 全局 ThreadSpace MUST 通过特殊标志（如 `IsGlobal`）与目录绑定的 ThreadSpace 区分。
- **FR-005**: REPL 启动时 MUST 显示欢迎头部，包含：应用名称/版本、当前活跃 Agent 名称、当前 ThreadSpace 名称。
- **FR-006**: 提示符 MUST 以简洁的上下文标识 + 输入指示符呈现。
  - **样式示例**: 全局模式为 `[global] > ` (蓝白风格)；项目模式为 `[project-name] > ` (青色风格)。
  - **截断逻辑**: 若项目名称超过 20 个字符，MUST 截断显示（如 `very-long-project-na...`）。
- **FR-007**: REPL MUST 支持以下斜杠命令：
  - `/help`: 显示结构化帮助表格。
  - `/new`: 在当前 ThreadSpace 下创建新会话。
  - `/resume`: 加载当前 ThreadSpace 中最近一次活跃会话。
  - `/cd <path>`: 切换到指定目录绑定的 ThreadSpace。
  - `/home`: 切换回全局 ThreadSpace（等同于不带参数的 `/cd`）。
  - `/clear`: 清除屏幕内容。
  - `/quit` / `/exit`: 干净退出 REPL。
- **FR-008**: 用户输入 `/new` 时，系统 MUST 在当前 ThreadSpace 下创建新会话，并清空当前显示的对话历史。
- **FR-009**: 用户输入 `/resume` 时，系统 MUST 加载当前 ThreadSpace 中最近一次活跃会话的消息历史并继续对话。
- **FR-010**: 用户输入 `/cd <path>` 时，系统 MUST 自动创建或复用与该路径绑定的 ThreadSpace，并切换到该 ThreadSpace 的最近会话（不存在则创建新会话）。
- **FR-011**: 用户输入未知斜杠命令时，系统 MUST 显示友好提示，不崩溃也不将其当作对话消息发送给 Agent。

### Key Entities

- **GlobalThreadSpace**: 全局默认的 ThreadSpace，无目录绑定，系统自动创建和维护，通过 `IsGlobal` 标志标识。持有跨项目的通用对话会话。
- **DirectoryThreadSpace**: 绑定具体本地目录的 ThreadSpace，继承现有设计，用于项目级工作流。Agent 工具操作以绑定目录为根。
- **ReplContext**: REPL 运行时内部状态，跟踪当前活跃的 ThreadSpace ID、Session ID 及提示符显示信息。随 `/cd`、`/new`、`/resume` 命令动态更新。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 用户从安装到第一条 AI 回复的操作步骤不超过 2 步（运行 `claw` + 输入消息）。
- **SC-002**: REPL 启动到提示符出现的等待时间不超过 3 秒（含数据库初始化的冷启动）。
- **SC-003**: `/resume` 命令在有历史会话的情况下成功恢复消息上下文的成功率达到 100%。
- **SC-004**: 所有内置斜杠命令执行完毕后，用户无需重启 REPL 即可继续对话（零中断率）。
- **SC-005**: 在全局 ThreadSpace 和目录绑定 ThreadSpace 之间切换只需 1 条命令（`/cd <path>` 或 `/home`）。

## Assumptions

- 全局 ThreadSpace 使用系统保留的固定标识（不由用户命名），在每次启动时由系统自动确保其存在。
- `claw` 不带参数时的默认 REPL 行为通过将现有 `chat` 命令逻辑提升为根命令默认处理器实现，不影响现有子命令路由。
- `spaces`、`list`、`history` 等管理子命令的功能保持不变，本次改动仅涉及 ThreadSpace 数据模型和 REPL 交互体验。
- `BoundFolderPath` 的可选化不要求迁移已有数据库记录；现有已有 `BoundFolderPath` 的记录保持原样。
- CLI 的 Markdown 渲染和流式输出能力（基于 Spectre.Console）保持现有实现，不在本次范围内重写。
- 多 Agent 可用且无默认配置时，自动选择注册表中的第一个 Agent（与现有降级逻辑一致）。
