namespace ClawSharp.Lib.Configuration;

/// <summary>
/// 引导配置数据模型，用于收集用户输入。
/// </summary>
public sealed class BootstrapConfig
{
    /// <summary>
    /// 工作区根目录。
    /// </summary>
    public string WorkspaceRoot { get; set; } = ".";

    /// <summary>
    /// 数据存放路径。
    /// </summary>
    public string DataPath { get; set; } = ".clawsharp";

    /// <summary>
    /// 默认提供商 ID。
    /// </summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>
    /// 提供商类型。
    /// </summary>
    public string ProviderType { get; set; } = "openai-responses";

    /// <summary>
    /// provider 基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// provider 默认模型。
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// 可选请求路径覆盖。
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 是否支持 Responses API。
    /// </summary>
    public bool SupportsResponses { get; set; }

    /// <summary>
    /// 是否支持 Chat Completions。
    /// </summary>
    public bool SupportsChatCompletions { get; set; }

    /// <summary>
    /// 提供商 API Key。
    /// </summary>
    public string? ApiKey { get; set; }
}
