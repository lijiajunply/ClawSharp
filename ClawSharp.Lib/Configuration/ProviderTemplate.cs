namespace ClawSharp.Lib.Configuration;

/// <summary>
/// 模型提供商模板，用于向导中的选择列表。
/// </summary>
public sealed class ProviderTemplate
{
    /// <summary>
    /// 显示名称（如 OpenAI）。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 提供商 ID（如 openai）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 实现类型（如 openai-responses）。
    /// </summary>
    public string Type { get; set; } = "stub";
}
