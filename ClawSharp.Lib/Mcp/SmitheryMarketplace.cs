using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// Smithery 市场上的 MCP server 概览信息。
/// </summary>
public sealed record SmitheryServer(
    [property: JsonPropertyName("displayName")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("qualifiedName")] string QualifiedName,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("homepage")] string? Homepage,
    [property: JsonPropertyName("useCount")] int? DownloadCount,
    [property: JsonPropertyName("verified")] bool Verified
);

/// <summary>
/// 搜索结果包装类。
/// </summary>
internal sealed class SmitherySearchResponse
{
    [JsonPropertyName("servers")]
    public List<SmitheryServer> Servers { get; set; } = [];
}

/// <summary>
/// Smithery 市场上的 MCP server 详情。
/// </summary>
public sealed record SmitheryServerDetail(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("qualifiedName")] string QualifiedName,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("homepage")] string? Homepage,
    [property: JsonPropertyName("useCount")] int? DownloadCount,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("readme")] string? Readme,
    [property: JsonPropertyName("mcpConfig")] SmitheryMcpConfig? McpConfig
);

/// <summary>
/// Smithery 提供的 MCP 配置片段。
/// </summary>
public sealed record SmitheryMcpConfig(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("args")] string[] Args,
    [property: JsonPropertyName("env")] Dictionary<string, SmitheryEnvVar>? Env
);

/// <summary>
/// 环境变量描述。
/// </summary>
public sealed record SmitheryEnvVar(
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("required")] bool Required = true
);

/// <summary>
/// Smithery 市场客户端接口。
/// </summary>
public interface ISmitheryClient
{
    /// <summary>
    /// 搜索市场上的 MCP server。
    /// </summary>
    /// <param name="query">搜索关键词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>server 列表。</returns>
    Task<IReadOnlyList<SmitheryServer>> SearchServersAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定的 MCP server 详情。
    /// </summary>
    /// <param name="qualifiedName">server 完整名称（如 owner/name）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>详情对象。</returns>
    Task<SmitheryServerDetail> GetServerAsync(string qualifiedName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Smithery 市场客户端实现。
/// </summary>
public sealed class SmitheryClient : ISmitheryClient
{
    private readonly HttpClient _httpClient;
    private readonly SmitheryOptions _options;

    public SmitheryClient(HttpClient httpClient, ClawOptions options)
    {
        _httpClient = httpClient;
        _options = options.Smithery;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SmitheryServer>> SearchServersAsync(string? query, CancellationToken cancellationToken = default)
    {
        var url = "servers";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"?q={Uri.EscapeDataString(query)}";
        }

        var results = await _httpClient.GetFromJsonAsync<SmitherySearchResponse>(url, cancellationToken).ConfigureAwait(false);
        return results?.Servers ?? [];
    }

    /// <inheritdoc />
    public async Task<SmitheryServerDetail> GetServerAsync(string qualifiedName, CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<SmitheryServerDetail>($"servers/{qualifiedName}", cancellationToken).ConfigureAwait(false);
        return result ?? throw new KeyNotFoundException($"Smithery server '{qualifiedName}' was not found.");
    }
}
