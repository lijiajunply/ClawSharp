# Data Model: Dynamic Agents, Skills, and MCP Support

## 1. 动态加载实体扩展 (Definition Extensions)

### DynamicSourceType (Enum)
用于标识定义文件的来源，决定冲突处理策略。
- `BuiltIn`: 内置核心定义。
- `Workspace`: 当前项目工作区定义（`workspace/`）。
- `User`: 用户个人主目录定义（`~/.agent`, `~/.skills`）。

### AgentDefinition / SkillDefinition (Fields)
- `Source`: `DynamicSourceType`
- `OriginalId`: `string` (原始 YAML 中定义的 ID)
- `SourcePath`: `string` (磁盘文件绝对路径)

## 2. MCP 核心实体 (MCP Core Entities)

### McpServerConfig (JSON Structure)
存储在 `~/.clawsharp/mcp.json`。
```json
{
  "mcpServers": {
    "sqlite-explorer": {
      "command": "node",
      "args": ["path/to/server.js"],
      "env": {
        "DB_PATH": "/path/to/db"
      }
    }
  }
}
```

### McpServerDefinition (Class)
- `Name`: `string`
- `Command`: `string`
- `Arguments`: `string[]`
- `EnvironmentVariables`: `IDictionary<string, string>`

### McpConnectionStatus (Enum)
- `Disconnected`
- `Connecting`
- `Connected`
- `Error`

### McpToolDefinition (Class)
适配现有工具系统的工具映射。
- `ServerName`: `string`
- `Name`: `string` (MCP 服务器返回的原始名称)
- `Description`: `string`
- `InputSchema`: `JsonDocument` (JSON Schema)

## 3. 协议消息 (MCP Protocol Messages)

基于 JSON-RPC 2.0 规范的内部契约。

### McpRequest (Internal)
- `Jsonrpc`: "2.0"
- `Id`: `string|number`
- `Method`: `string`
- `Params`: `object`

### McpResponse (Internal)
- `Jsonrpc`: "2.0"
- `Id`: `string|number`
- `Result`: `object`
- `Error`: `McpError` (可选)

## 4. 状态流转 (State Transitions)

### MCP 服务器生命周期
1. `Idle`: 等待初始化。
2. `Starting`: `Process.Start()` 调用中。
3. `Handshake`: 发送 `initialize` 请求。
4. `Running`: 成功获取工具列表。
5. `Stopped`: 进程退出或手动关闭。
