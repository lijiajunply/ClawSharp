# Quickstart: 动态 Agent, 技能与 MCP 支持

本文档指导用户如何快速利用 ClawSharp 的动态扩展能力。

## 1. 动态录入 Agent

1. 在您的用户目录下创建 `~/.agent` 目录。
2. 创建一个新的 Markdown 文件，例如 `~/.agent/my-helper.md`。
3. 添加以下 YAML 前置配置：
   ```markdown
   ---
   id: my-helper
   name: "我的助手"
   description: "这是一个动态加载的助手"
   ---
   # 我的助手
   我是您的个性化助手。
   ```
4. 启动 `ClawSharp.CLI`，您将在可用 Agent 列表中看到“我的助手”。

## 2. 动态录入技能 (带有自动命名空间)

1. 创建 `~/.skills` 目录。
2. 添加一个技能定义文件，例如 `~/.skills/calculator.md`。
3. 如果此技能与内置技能冲突，系统会将其 ID 修改为 `user.calculator`。
4. 在会话中，Agent 即可调用该自定义技能提供的工具。

## 3. 集成 MCP 工具

1. 创建 `~/.clawsharp/mcp.json` 配置文件。
2. 配置一个本地 MCP 服务器，例如使用 SQLite 工具：
   ```json
   {
     "mcpServers": {
       "sqlite": {
         "command": "uvx",
         "args": ["mcp-server-sqlite", "--db-path", "/Users/me/test.db"]
       }
     }
   }
   ```
3. 启动应用，内核将自动建立连接。
4. 您可以使用支持工具调用的 Agent 来操作 SQLite 数据库，无需编写任何 C# 代码。

## 4. 实时刷新

在应用程序运行期间，您可以随时修改 `~/.agent` 或 `~/.skills` 中的文件。ClawSharp 会检测到变更并自动重新加载注册表。
