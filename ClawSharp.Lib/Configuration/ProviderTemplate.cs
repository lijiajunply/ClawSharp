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

    /// <summary>
    /// provider 基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 默认模型名。
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// 可选的请求路径覆盖。
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 是否需要 API Key。
    /// </summary>
    public bool RequiresApiKey { get; set; } = true;

    /// <summary>
    /// 是否支持 Responses API。
    /// </summary>
    public bool SupportsResponses { get; set; }

    /// <summary>
    /// 是否支持 Chat Completions。
    /// </summary>
    public bool SupportsChatCompletions { get; set; }
}
