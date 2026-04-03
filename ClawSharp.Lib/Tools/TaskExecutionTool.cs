using System.Text.Json;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// 用于分步执行任务的工具。
/// </summary>
public sealed class TaskExecutionTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "apply_change",
        "在代码库中执行具体的、经过批准的修改任务。通过替换旧字符串为新字符串来实现精确编辑，确保一次只做一个小改动。",
        JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "task_id": {
              "type": "string",
              "description": "tasks.md 中对应的任务标识。"
            },
            "file_path": {
              "type": "string",
              "description": "要修改的文件路径。"
            },
            "instruction": {
              "type": "string",
              "description": "对该改动的描述。"
            },
            "old_string": {
              "type": "string",
              "description": "要替换的原始字符串（必须精确匹配）。"
            },
            "new_string": {
              "type": "string",
              "description": "替换后的新内容。"
            }
          },
          "required": ["task_id", "file_path", "instruction", "old_string", "new_string"]
        }
        """),
        null,
        ToolCapability.FileWrite);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var taskId = arguments.GetProperty("task_id").GetString() ?? string.Empty;
        var filePath = arguments.GetProperty("file_path").GetString() ?? string.Empty;
        var oldString = arguments.GetProperty("old_string").GetString() ?? string.Empty;
        var newString = arguments.GetProperty("new_string").GetString() ?? string.Empty;

        var fullPath = Path.GetFullPath(Path.Combine(context.WorkspaceRoot, filePath));

        if (!File.Exists(fullPath))
        {
            return ToolInvocationResult.Failure(Definition.Name, $"File not found: {filePath}");
        }

        var content = await File.ReadAllTextAsync(fullPath, context.CancellationToken).ConfigureAwait(false);
        
        // 精确匹配检查
        int count = 0;
        int index = content.IndexOf(oldString, StringComparison.Ordinal);
        while (index != -1)
        {
            count++;
            index = content.IndexOf(oldString, index + oldString.Length, StringComparison.Ordinal);
        }

        if (count == 0)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"Could not find exact match for 'old_string' in {filePath}.");
        }
        
        if (count > 1)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"'old_string' is ambiguous; found {count} occurrences in {filePath}. Please provide more context.");
        }

        var updatedContent = content.Replace(oldString, newString, StringComparison.Ordinal);
        await File.WriteAllTextAsync(fullPath, updatedContent, context.CancellationToken).ConfigureAwait(false);

        return ToolInvocationResult.Success(Definition.Name, $"Successfully applied change for task {taskId} in {filePath}.");
    }
}
