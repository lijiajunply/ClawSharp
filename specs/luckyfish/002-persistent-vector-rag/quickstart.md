# Quickstart: Persistent Vector Store and Automated RAG

## Setup

1. **安装依赖**:
   确保项目引用了 `FastEmbed.Net` 和 `Microsoft.Data.Sqlite`。
2. **配置 Embedding**:
   在 `appsettings.json` 或 `.env` 中指定：
   ```json
   {
     "ClawSharp": {
       "Memory": {
         "DefaultProvider": "SqliteVss",
         "Embedding": {
           "Type": "Local",
           "Model": "BAAI/bge-small-zh-v1.5"
         }
       }
     }
   }
   ```

## Usage Example (Library)

```csharp
var kernel = builder.Build();
var runtime = kernel.GetRequiredService<IClawRuntime>();

// 自动 RAG 注入：只需正常调用 RunTurnStreamingAsync
// 只要 Agent 配置了关联的 Memory 命名空间，系统会自动检索知识
await foreach (var delta in runtime.RunTurnStreamingAsync(sessionId, userInput))
{
    Console.Write(delta.Content);
}
```

## Performance Note

首次使用本地模型会下载约 50MB 的权重文件，请确保网络连接正常。
