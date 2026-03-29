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
- 基于 SQLite 的 session store、prompt history 和 session event store
- 单 agent runtime loop，支持工具调用与历史持久化
- 本地 worker 抽象，包含 loopback worker 与 stdio JSON-RPC 传输形态
- Provider 抽象，当前内置：
  - `StubModelProvider`，用于测试和离线回退
  - `OpenAiResponsesModelProvider`
  - `OpenAiCompatibleChatModelProvider`

核心 runtime 接口位于 [`IClawRuntime`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Runtime/RuntimeContracts.cs#L50)，目前支持：

- `StartSessionAsync`
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
  - 模型 provider 抽象
  - OpenAI Responses API 支持
  - OpenAI-compatible Chat Completions 支持
- `Runtime`
  - Sessions
  - Prompt history
  - Session events
  - Agent worker 集成
  - 单 agent turn orchestration

## Provider 支持

ClawSharp 当前支持三种 provider 模式：

1. `stub`
   用于测试和显式离线模式。

2. `openai-responses`
   当前推荐的生产路径。使用 OpenAI Responses API，支持：
   - 流式文本输出
   - 自定义函数工具调用
   - 工具结果回传后的继续推理

3. `openai-chat-compatible`
   用于支持暴露 OpenAI 风格 `chat/completions` API 的兼容服务。

Provider resolver 实现在 [`ModelProviderResolver`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/ModelProviderContracts.cs#L174)。

具体 provider 实现在：

- [`OpenAiResponsesModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L330)
- [`OpenAiCompatibleChatModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L458)
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
```

Agent 文件使用带 YAML frontmatter 的 Markdown。正文部分可以承载更长的说明、规则或示例。  
Skill 文件采用相同的基本模式。

## 配置

配置加载顺序如下：

1. `appsettings.json`
2. `appsettings.Local.json`
3. `.env`
4. 环境变量
5. 传入 `AddClawSharp` 的运行时 overrides

主配置对象是 [`ClawOptions`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Configuration/ClawOptions.cs#L5)。

重要配置分组包括：

- `Runtime`
- `Agents`
- `Tools`
- `Mcp`
- `Memory`
- `Embedding`
- `Sessions`
- `History`
- `Providers`
- `Worker`
- `WorkspacePolicy`

### `appsettings.json` 示例

```json
{
  "Runtime": {
    "WorkspaceRoot": ".",
    "DataPath": ".clawsharp"
  },
  "Sessions": {
    "DatabasePath": ".clawsharp/clawsharp.db"
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
        "Name": "compat",
        "Type": "openai-chat-compatible",
        "BaseUrl": "https://your-compatible-endpoint.example",
        "DefaultModel": "your-model",
        "SupportsChatCompletions": true
      }
    ]
  }
}
```

### `.env` 示例

```dotenv
Providers__Models__0__ApiKey=your_openai_api_key
Providers__Models__1__ApiKey=your_compatible_api_key
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
- 为 agent 定义增加 provider 级别选择
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
