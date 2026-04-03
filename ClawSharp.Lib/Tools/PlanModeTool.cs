using System.Text.Json;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// 用于切换 Session 运行模式（Chat/Plan）的工具。
/// </summary>
public sealed class PlanModeTool(IServiceProvider serviceProvider) : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "enter_plan_mode",
        "切换到 Plan 模式。在该模式下，只能执行只读操作和编写实施计划，不能直接修改代码或系统状态。适用于大型重构、复杂任务或涉及破坏性操作的场景。",
        JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "reason": {
              "type": "string",
              "description": "进入 Plan 模式的原因 or 目标。"
            }
          },
          "required": ["reason"]
        }
        """),
        null,
        ToolCapability.None);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var reason = arguments.GetProperty("reason").GetString() ?? string.Empty;
        
        var runtime = serviceProvider.GetRequiredService<IClawRuntime>();
        await runtime.UpdateSessionModeAsync(new SessionId(context.SessionId), SessionMode.Plan, context.CancellationToken).ConfigureAwait(false);
        
        return ToolInvocationResult.Success(Definition.Name, $"已进入 Plan 模式。原因: {reason}\n请开始调查并编写详细的实施计划（plan.md）。在 Plan 模式下，写操作将被拦截。");
    }
}

/// <summary>
/// 用于从 Plan 模式切回 Chat 模式并提交计划的工具。
/// </summary>
public sealed class ExitPlanModeTool(IServiceProvider serviceProvider) : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "exit_plan_mode",
        "完成计划后退出 Plan 模式，切回 Chat 模式。该操作通常意味着计划已就绪并请求用户批准执行。",
        JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "plan_path": {
              "type": "string",
              "description": "生成的实施计划文件路径。"
            }
          },
          "required": ["plan_path"]
        }
        """),
        null,
        ToolCapability.None);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var planPath = arguments.GetProperty("plan_path").GetString() ?? "plan.md";
        
        var runtime = serviceProvider.GetRequiredService<IClawRuntime>();
        await runtime.UpdateSessionModeAsync(new SessionId(context.SessionId), SessionMode.Chat, context.CancellationToken).ConfigureAwait(false);
        
        return ToolInvocationResult.Success(Definition.Name, $"已退出 Plan 模式并切回 Chat 模式。计划路径: {planPath}\n请等待用户确认计划。");
    }
}
