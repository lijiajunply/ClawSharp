using System.Diagnostics;
using System.Text.Json;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 表示单个本地模型服务的探测结果。
/// </summary>
/// <param name="Available">服务是否可访问。</param>
/// <param name="BaseUrl">探测到的基础地址。</param>
/// <param name="Models">探测到的模型列表。</param>
public sealed record LocalModelServiceDiscovery(
    bool Available,
    string? BaseUrl,
    IReadOnlyList<string> Models);

/// <summary>
/// 表示运行时依赖与本地模型服务的统一探测结果。
/// </summary>
/// <param name="PlaywrightInstalled">Playwright Chromium 是否已安装。</param>
/// <param name="Ollama">Ollama 探测结果。</param>
/// <param name="LlamaEdge">LlamaEdge 探测结果。</param>
public sealed record EnvironmentDiscoveryResult(
    bool PlaywrightInstalled,
    LocalModelServiceDiscovery Ollama,
    LocalModelServiceDiscovery LlamaEdge)
{
    /// <summary>
    /// 是否至少发现了一个可用的本地模型服务。
    /// </summary>
    public bool HasLocalModelProvider => Ollama.Available || LlamaEdge.Available;
}

/// <summary>
/// 负责探测运行时依赖与常见本地模型服务。
/// </summary>
public static class EnvironmentDiscoveryInspector
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true
    };

    /// <summary>
    /// 执行一次统一的环境探测。
    /// </summary>
    public static async Task<EnvironmentDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var playwrightInstalled = IsPlaywrightInstalled();
        var ollama = await DetectOllamaAsync(cancellationToken).ConfigureAwait(false);
        var llamaEdge = await DetectLlamaEdgeAsync(cancellationToken).ConfigureAwait(false);
        return new EnvironmentDiscoveryResult(playwrightInstalled, ollama, llamaEdge);
    }

    /// <summary>
    /// 判断 Playwright Chromium 是否可用。
    /// </summary>
    public static bool IsPlaywrightInstalled()
    {
        var playwrightCaches = ResolvePlaywrightCachePaths();
        return playwrightCaches.Any(path =>
            Directory.Exists(path) &&
            Directory.EnumerateDirectories(path, "chromium-*").Any());
    }

    /// <summary>
    /// 探测 Ollama 服务。
    /// </summary>
    public static async Task<LocalModelServiceDiscovery> DetectOllamaAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("OLLAMA_HOST"),
            "http://127.0.0.1:11434",
            "http://localhost:11434"
        };

        return await DetectAsync(
            candidates,
            "api/tags",
            ParseOllamaModels,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 探测 LlamaEdge OpenAI-compatible 服务。
    /// </summary>
    public static async Task<LocalModelServiceDiscovery> DetectLlamaEdgeAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("LLAMAEDGE_HOST"),
            "http://127.0.0.1:8080",
            "http://localhost:8080"
        };

        return await DetectAsync(
            candidates,
            "v1/models",
            ParseOpenAiCompatibleModels,
            cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<string> ResolvePlaywrightCachePaths()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright"));
        }
        else
        {
            paths.Add(Path.Combine(userHome, ".cache", "ms-playwright"));

            if (OperatingSystem.IsMacOS())
            {
                paths.Add(Path.Combine(userHome, "Library", "Caches", "ms-playwright"));
            }
        }

        return paths;
    }

    private static async Task<LocalModelServiceDiscovery> DetectAsync(
        IEnumerable<string?> candidates,
        string path,
        Func<string, IReadOnlyList<string>> parseModels,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in candidates)
        {
            var baseUrl = NormalizeBaseUrl(candidate);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                continue;
            }

            try
            {
                using var client = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute),
                    Timeout = TimeSpan.FromSeconds(1.5)
                };

                using var response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var models = parseModels(body);
                return new LocalModelServiceDiscovery(true, baseUrl, models);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Environment discovery failed for {baseUrl}: {ex.Message}");
            }
        }

        return new LocalModelServiceDiscovery(false, null, Array.Empty<string>());
    }

    private static string? NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = $"http://{trimmed}";
        }

        return trimmed.TrimEnd('/');
    }

    private static IReadOnlyList<string> ParseOllamaModels(string body)
    {
        using var document = JsonDocument.Parse(body, JsonOptions);
        if (!document.RootElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return modelsElement.EnumerateArray()
            .Select(model => model.TryGetProperty("name", out var name) ? name.GetString() : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ParseOpenAiCompatibleModels(string body)
    {
        using var document = JsonDocument.Parse(body, JsonOptions);
        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return dataElement.EnumerateArray()
            .Select(model => model.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray();
    }
}
