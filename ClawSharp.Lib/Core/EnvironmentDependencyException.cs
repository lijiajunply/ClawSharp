namespace ClawSharp.Lib.Core;

/// <summary>
/// 表示运行特定工具所需的外部环境或驱动缺失时抛出的异常。
/// </summary>
/// <param name="toolName">关联的工具名。</param>
/// <param name="message">异常详细消息。</param>
/// <param name="fixCommand">推荐的修复命令。</param>
public sealed class EnvironmentDependencyException(string toolName, string message, string? fixCommand = null) 
    : Exception(message)
{
    /// <summary>
    /// 关联的工具名。
    /// </summary>
    public string ToolName { get; } = toolName;

    /// <summary>
    /// 推荐的修复命令。
    /// </summary>
    public string? FixCommand { get; } = fixCommand;
}
