namespace ClawSharp.Lib.Core;

/// <summary>
/// 表示配置、frontmatter 或运行时输入不满足库约束时抛出的异常。
/// </summary>
/// <param name="message">异常消息。</param>
public sealed class ValidationException(string message) : Exception(message);
