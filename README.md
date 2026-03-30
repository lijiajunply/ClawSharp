# ClawSharp

[English](./README_en.md)

ClawSharp 是一个使用 C# 编写、面向 `.NET 10` 的本地优先 AI 应用内核。

项目的长期目标，是在底层能力上尽量对齐 OpenClaw 的核心思路：

- MCP 集成
- 基于 Markdown 的 agents
- 基于 Markdown 的 skills
- 内置本地工具系统
- 向量记忆与检索
- 基于 JSON 和 `.env` 的本地配置
- 先完成可复用的 Library 内核，再承载 CLI 和 Web 应用

当前仓库的重点是 `ClawSharp.Lib`。到目前为止，这个库已经具备可运行的 runtime 骨架、持久化 session、provider 抽象、工具执行、memory，以及真实 OpenAI / 兼容 API provider 支持。

## 当前进度

`ClawSharp.Lib` 已经实现的能力：

- 通过 [`AddClawSharp(...)`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs#L26) 提供统一 DI 入口
- Agent Markdown 解析与注册表加载
- Skill Markdown 解析与注册表加载
- 内置工具：
  - `shell.run`
  - `file.read`
  - `file.write`
  - `file.list`
  - `system.info`
  - `system.processes`
  - `search.text`
  - `search.files`
- 工具 capability 与权限模型
- MCP client 骨架与进程型 session
- Memory 抽象与本地默认实现
- ThreadSpace 容器层，支持绑定目录与默认 `init` 工作单元
- 基于 SQLite 的 session store、prompt history 和 session event store
- 单 agent runtime loop，支持工具调用与历史持久化
- 本地 worker 抽象，包含 loopback worker 与 stdio JSON-RPC 传输形态
- Provider 抽象，当前内置：
  - `StubModelProvider`，用于测试和离线回退
  - `OpenAiResponsesModelProvider`
  - `OpenAiCompatibleChatModelProvider`

核心 runtime 接口位于 [`IClawRuntime`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Runtime/RuntimeContracts.cs#L50)，目前支持：

- `StartSessionAsync`
- `StartSessionAsync(StartSessionRequest)`
- `AppendUserMessageAsync`
- `RunTurnAsync`
- `GetHistoryAsync`
- `CancelSessionAsync`

## 架构概览

当前库主要由以下几个子系统组成：

- `Configuration`
  - 强类型配置对象
  - JSON + `.env` + 环境变量加载
- `Agents`
  - Markdown frontmatter 解析
  - 基于文件系统的定义加载
  - Registry 模型
- `Skills`
  - Markdown skill 定义
  - Registry 模型
- `Tools`
  - Tool schema 与 executor 抽象
  - 权限、allowlist 和执行上下文
- `Mcp`
  - MCP catalog 与 client/session 骨架
- `Memory`
  - Embedding 抽象
  - Vector store 抽象
  - 本地默认实现
- `Providers`
  - 支持 structured content blocks 的模型 provider 抽象
  - OpenAI Responses API 支持
  - OpenAI-compatible Chat Completions 支持
- `Runtime`
  - ThreadSpaces
  - Sessions
  - Prompt history
  - Session events
  - Agent worker 集成
  - 单 agent turn orchestration

## Project / ThreadSpace / Session

ClawSharp 现在区分三层概念：

- `Project`
  - 用于模板脚手架和内容目录生成
- `ThreadSpace`
  - 绑定某个文件夹的聊天/工作容器，或者不绑定任何目录的全局容器（`global`）
  - 一个 ThreadSpace 可以承载多个 session
  - 系统会自动确保存在默认 `global` ThreadSpace，用于跨项目的通用对话
- `session`
  - ThreadSpace 内的实际对话执行单元

### CLI 体验

ClawSharp 提供了功能丰富的命令行界面。直接运行 `claw` 即可进入默认的 REPL 对话模式，或者通过子命令管理系统。

#### REPL 模式
进入对话后，你可以像在普通聊天应用中一样输入文字，也可以使用斜杠命令进行控制：

- `/help`: 查看帮助
- `/new`: 在当前空间开启新会话
- `/resume`: 恢复当前空间的上一个会话（支持查看最后几条历史）
- `/cd <path>`: 切换到特定目录的工作空间（ThreadSpace）
- `/home`: 返回全局默认空间
- `/init`: 在当前目录初始化一个 `agent.md` 定义文件
- `/init-proj`: 基于模板交互式创建新项目脚手架
- `/clear`: 清屏
- `/quit, /exit`: 退出 REPL

**交互特性：**
- **智能提示**：输入时自动显示灰色的“幽灵文字”建议，按 `Tab` 或 `→` 键快速补全。
- **持久化历史**：支持上下方向键翻阅历史命令，历史记录跨会话保存。
- **流式输出**：Agent 的回答会以流式打字机效果实时显示。

#### 管理命令
除了 REPL 内部，你也可以直接从命令行调用子命令：

- `claw chat [agent-id]`: 以指定 Agent 开启对话（默认进入 REPL）
- `claw list`: 列出当前空间的所有会话
- `claw history <session-id>`: 以 Markdown 格式美化查看特定会话的完整历史
- `claw agents`: 查看所有已注册的 Agent 及其解析后的 Provider/Model 状态
- `claw skills`: 查看所有已注册的 Skill 列表
- `claw spaces`: 管理 ThreadSpace
  - `list`: 列出所有工作空间
  - `add <name> <path>`: 添加新的空间绑定
  - `show <id/name>`: 查看空间详情及其包含的会话
- `claw config`: 管理本地配置（存储于 `appsettings.Local.json`）
  - `list [--all]`: 查看当前生效的配置项（敏感信息会自动打码）
  - `set <key> [value]`: 设置配置项（不提供 value 时会进入安全输入模式）
  - `get <key>`: 获取特定配置
  - `reset`: 重置配置

## Provider 支持

ClawSharp 当前支持多种模型 Provider：

1. `stub`: 离线测试模式。
2. `openai-responses`: **推荐路径**。支持 OpenAI 最新的 Responses API，具备原生的流式工具调用回环。
3. `openai-chat-compatible`: 兼容标准的 `chat/completions` 接口，适用于 DeepSeek, Groq, local LLMs 等。
4. `gemini-openai-compatible`: 针对 Google Gemini 的 OpenAI 兼容端点优化。
5. `anthropic-messages`: 支持 Claude 的 Messages API，包含原生的流式输出与工具调用模型。

Provider resolver 实现在 [`ModelProviderResolver`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/ModelProviderContracts.cs#L174)。

具体 provider 实现在：

- [`OpenAiResponsesModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L330)
- [`OpenAiCompatibleChatModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L458)
- [`GeminiCompatibleChatModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs)
- [`AnthropicMessagesModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/AnthropicProviders.cs)
- [`StubModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/ModelProviderContracts.cs#L235)

## Agent 与 Skill 目录约定

当前约定的目录结构如下：

```text
workspace/
  agents/
    <agent-id>/
      agent.md
  skills/
    <skill-id>/
      SKILL.md
      assets/
      scripts/
  project-templates/
    <template-id>/
      template.md
      ...
```

Agent 文件使用带 YAML frontmatter 的 Markdown。正文部分可以承载更长的说明、规则或示例。  
Skill 文件采用相同的基本模式。
项目模板目录用于 `ClawSharp.Lib.Projects` 的新建项目脚手架能力；模板元数据位于 `template.md`，其余文件会按相对路径生成到新项目中。

Agent frontmatter 现在支持可选的 `provider` 字段，用来为单个 agent 指定模型厂商；未填写时会回退到 `Providers:DefaultProvider`。`model` 也可以留空，此时优先使用该 provider 的 `DefaultModel`。

## 配置

ClawSharp 使用多层级配置系统：

1. `appsettings.json`: 默认全局配置。
2. `appsettings.Local.json`: 本地持久化配置（可通过 `claw config` 修改）。
3. `.env`: 用于存储 API Key 等敏感信息。
4. 环境变量。

主配置对象是 [`ClawOptions`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Configuration/ClawOptions.cs#L5)。

### `appsettings.json` 示例

```json
{
  "Runtime": {
    "WorkspaceRoot": ".",
    "DataPath": ".clawsharp"
  },
  "Databases": {
    "Sqlite": { "DatabasePath": ".clawsharp/clawsharp.db" },
    "DuckDb": { "Enabled": true, "DatabasePath": ".clawsharp/analytics.duckdb" }
  },
  "Providers": {
    "DefaultProvider": "openai",
    "Models": [
      {
        "Name": "openai",
        "Type": "openai-responses",
        "BaseUrl": "https://api.openai.com",
        "DefaultModel": "gpt-4.1-mini",
        "SupportsResponses": true
      },
      {
        "Name": "claude",
        "Type": "anthropic-messages",
        "BaseUrl": "https://api.anthropic.com",
        "DefaultModel": "claude-3-5-sonnet-20241022"
      }
    ]
  }
}
```

### `.env` 示例

```dotenv
Providers__Models__0__ApiKey=your_openai_api_key
Providers__Models__1__ApiKey=your_anthropic_api_key
```

### `agent.md` frontmatter 示例

```yaml
---
id: planner
name: Planner
description: Multi-provider planner
provider: claude
model: ""
system_prompt: You are a careful planner.
memory_scope: workspace
version: v1
---
```

## 如何使用

这个库的设计目标，是成为未来 CLI 和 Web 前端共享的应用内核。

最基础的 DI 接入方式如下：

```csharp
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddClawSharp(builder =>
{
    builder.BasePath = Directory.GetCurrentDirectory();
});

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IClawRuntime>();

await runtime.InitializeAsync();

var session = await runtime.StartSessionAsync("planner");
await runtime.AppendUserMessageAsync(session.Record.SessionId, "Hello");

var result = await runtime.RunTurnAsync(session.Record.SessionId);
var history = await runtime.GetHistoryAsync(session.Record.SessionId);
```

如果要使用项目脚手架服务，可以直接从 DI 获取 `IProjectScaffolder`：

```csharp
using ClawSharp.Lib.Projects;

var scaffolder = provider.GetRequiredService<IProjectScaffolder>();

var createResult = await scaffolder.CreateProjectAsync(
    new CreateProjectRequest(
        "paper",
        "My Research Project",
        "projects/my-research",
        new Dictionary<string, string>
        {
            ["author"] = "Lucky Fish",
            ["project_summary"] = "用于整理研究资料、写作计划与实验记录。"
        }));
```

默认模板目录是 `workspace/project-templates`。  
每种模板一个目录，例如：

```text
workspace/
  project-templates/
    paper/
      template.md
      docs/
        outline.md
      references/
        bibliography.md
```

其中 `template.md` 使用 YAML frontmatter 描述模板元数据，正文会追加到统一生成的 `README.md` 中：

```md
---
id: paper
name: Paper Project
description: 用于组织论文写作和资料整理的项目模板。
version: v1
directories:
  - docs
  - references
variables:
  author: Unknown Author
---
## 研究背景
记录研究问题、关键假设和参考资料。
```

模板文件名与文件内容都支持简单变量替换，例如 `{{project_name}}`、`{{project_type}}`、`{{created_at}}` 以及调用方传入的自定义变量。  
无论模板是否声明，最终项目都会生成带统一骨架的 `README.md`，用于说明这是什么项目。

## 安全与权限 (Security & Permissions)

ClawSharp 实现了基于 **Capability** 的权限模型和 **Workspace Policy** 安全边界：

- **Capability 掩码**: 每个工具都声明其所需的能力（如 `FileRead`, `ShellExecute`）。
- **Workspace Policy**: 在 `appsettings.json` 中定义，作为所有 Agent 的“权限天花板”。Agent 声明的权限将与工作空间策略取**交集**。
- **强制工具 (Mandatory Tools)**: 可以配置全局强制注入的工具，无论 Agent 是否声明，它们都会出现在 Agent 的执行计划中。
- **按需授权 (Just-In-Time Permissions)**: 当 Agent 尝试使用超出当前权限范围的工具时，系统会通过交互式提示请求用户即时授权（需实现 `IPermissionUI`）。

配置示例 (`appsettings.Local.json`):

```json
{
  "ClawSharp": {
    "WorkspacePolicy": {
      "Permissions": {
        "Capabilities": "FileRead, NetworkAccess, VersionControl"
      },
      "MandatoryTools": ["audit_logger"]
    }
  }
}
```

## 测试

运行测试：

```bash
/usr/local/share/dotnet/dotnet test ClawSharp.slnx
```

当前测试覆盖包括：

- agent 与 skill 的 Markdown 解析
- registries
- tool security 与权限合并
- 配置优先级与 provider 绑定
- SQLite session/history/event 存储
- memory indexing
- MCP 启动失败与超时行为
- provider 解析
- Responses API 流式解析
- OpenAI-compatible chat 流式解析
- 基于 stub 和真实 provider 风格 stream 的 runtime integration

## 当前还不是什么

ClawSharp 目前还不是：

- 一个完成版 CLI
- 一个完成版 Web UI
- 一个多 agent 协作平台
- 一个生产级 MCP host/server
- 一个完整的外部 worker 可执行程序
- 一个每轮都自动检索的完整 RAG 系统

这些都在规划中，但当前重点仍然是可复用的应用内核。

## 路线图

接下来的自然演进方向包括：

- 增加真实外部 worker 可执行程序，跑通 stdio JSON-RPC
- 在 `IClawRuntime` 之上加第一版 CLI
- 增加 `.env.example` 与更完整的示例配置
- 继续增强 provider 支持，包括重试、错误处理和更多 wire shape
- 深化 MCP 集成，让 MCP tools/resources 更直接参与 prompt 组装
- 把 memory 扩展成可选的 retrieval pipeline

## 项目方向

项目最初的目标，是先把 AI 本地应用最底层的内核能力打扎实，再逐步叠加 CLI 和 Web 层。  
当前仓库已经开始体现这个方向：

- 领域模型以 Library 为核心
- runtime 是可持久化、可测试的
- provider 层已经能连接真实外部 API
- tool loop 与 history 模型已经建立

这让 `ClawSharp.Lib` 成为了后续所有界面的基础。
