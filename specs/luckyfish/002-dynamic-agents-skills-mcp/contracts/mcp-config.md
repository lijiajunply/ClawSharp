# MCP Configuration Contract (mcp.json)

本文档定义了 `~/.clawsharp/mcp.json` 的配置结构，系统将依据此配置启动并连接本地 MCP 服务器。

## Schema 定义

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "mcpServers": {
      "type": "object",
      "patternProperties": {
        "^[a-zA-Z0-9_-]+$": {
          "type": "object",
          "properties": {
            "command": {
              "type": "string",
              "description": "启动服务器的可执行文件路径或名称（如 'node', 'python', 'docker'）"
            },
            "args": {
              "type": "array",
              "items": { "type": "string" },
              "description": "传递给命令的参数列表"
            },
            "env": {
              "type": "object",
              "additionalProperties": { "type": "string" },
              "description": "服务器运行时需要的环境变量"
            }
          },
          "required": ["command"]
        }
      }
    }
  }
}
```

## 默认路径
- **macOS**: `/Users/<user>/.clawsharp/mcp.json`
- **Linux**: `/home/<user>/.clawsharp/mcp.json`
- **Windows**: `C:\Users\<user>\.clawsharp\mcp.json`

## 兼容性说明
该格式与 **Claude Desktop** 的配置文件结构保持 100% 兼容。如果用户已在 Claude Desktop 中配置了 MCP 服务器，只需将其 `mcpServers` 部分复制到本文件中即可。

## 注意事项
1. `command` 必须在系统 PATH 中，或者是绝对路径。
2. 不支持交互式命令（如 `vim`, `less`）。
3. 环境变量中的路径推荐使用绝对路径以确保跨平台兼容。
