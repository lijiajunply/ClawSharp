using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// 描述一个可启动的 MCP server 配置。
/// </summary>
public sealed class McpServerDefinition
{
    /// <summary>
    /// server 名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 启动命令。
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 启动参数。
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// 进程环境变量。
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 等待 server 就绪的超时时间，单位为秒。默认值为 10。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 可选的就绪信号文本；设置后会等待标准输出中出现该文本。
    /// </summary>
    public string? ReadySignal { get; set; }

    /// <summary>
    /// server 暴露能力的本地摘要。
    /// </summary>
    public ToolCapability Capabilities { get; set; } = ToolCapability.None;
}

/// <summary>
/// MCP 工具描述。
/// </summary>
/// <param name="Name">工具名。</param>
/// <param name="Description">工具描述。</param>
/// <param name="InputSchema">输入 schema。</param>
public sealed record McpToolDescriptor(string Name, string Description, JsonElement? InputSchema = null);

/// <summary>
/// MCP 资源描述。
/// </summary>
/// <param name="Name">资源名。</param>
/// <param name="Uri">资源 URI。</param>
/// <param name="Description">资源描述。</param>
public sealed record McpResourceDescriptor(string Name, string Uri, string Description);

/// <summary>
/// MCP prompt 描述。
/// </summary>
/// <param name="Name">prompt 名称。</param>
/// <param name="Description">prompt 描述。</param>
public sealed record McpPromptDescriptor(string Name, string Description);

/// <summary>
/// MCP server 配置目录接口。
/// </summary>
public interface IMcpServerCatalog
{
    /// <summary>
    /// 获取全部 MCP server 定义。
    /// </summary>
    /// <returns>server 定义集合。</returns>
    IReadOnlyCollection<McpServerDefinition> GetAll();

    /// <summary>
    /// 按名称获取单个 MCP server 定义。
    /// </summary>
    /// <param name="name">server 名称。</param>
    /// <returns>匹配的 server 定义。</returns>
    McpServerDefinition Get(string name);
}

/// <summary>
/// 表示一个已连接的 MCP 会话。
/// </summary>
public interface IMcpSession : IAsyncDisposable
{
    /// <summary>
    /// server 名称。
    /// </summary>
    string ServerName { get; }

    /// <summary>
    /// 是否仍然连接。
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 当前会话发现的工具列表。
    /// </summary>
    IReadOnlyCollection<McpToolDescriptor> Tools { get; }

    /// <summary>
    /// 当前会话发现的资源列表。
    /// </summary>
    IReadOnlyCollection<McpResourceDescriptor> Resources { get; }

    /// <summary>
    /// 当前会话发现的 prompt 列表。
    /// </summary>
    IReadOnlyCollection<McpPromptDescriptor> Prompts { get; }
}

/// <summary>
/// MCP 会话管理器。
/// </summary>
public interface IMcpClientManager
{
    /// <summary>
    /// 建立或复用一个 MCP 会话。
    /// </summary>
    /// <param name="serverName">server 名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可用的 MCP 会话。</returns>
    Task<IMcpSession> ConnectAsync(string serverName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开一个 MCP 会话。
    /// </summary>
    /// <param name="serverName">server 名称。</param>
    Task DisconnectAsync(string serverName);

    /// <summary>
    /// 获取当前已连接的 MCP 会话。
    /// </summary>
    /// <returns>已连接会话集合。</returns>
    IReadOnlyCollection<IMcpSession> GetConnectedSessions();
}

/// <summary>
/// MCP host 的占位接口，为未来扩展预留。
/// </summary>
public interface IMcpHost
{
}

/// <summary>
/// 可将本地工具暴露给 MCP 的提供者接口。
/// </summary>
public interface IMcpExposedToolProvider
{
    /// <summary>
    /// 获取要暴露的工具集合。
    /// </summary>
    /// <returns>工具定义集合。</returns>
    IReadOnlyCollection<ToolDefinition> GetTools();
}

/// <summary>
/// 可将本地资源暴露给 MCP 的提供者接口。
/// </summary>
public interface IMcpExposedResourceProvider
{
    /// <summary>
    /// 读取指定资源。
    /// </summary>
    /// <param name="uri">资源 URI。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资源内容。</returns>
    Task<string> ReadAsync(string uri, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的 MCP server 目录实现。
/// </summary>
/// <param name="options">库配置。</param>
public sealed class McpServerCatalog(ClawOptions options) : IMcpServerCatalog
{
    private readonly Dictionary<string, McpServerDefinition> _servers =
        options.Mcp.Servers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<McpServerDefinition> GetAll() => _servers.Values.ToArray();

    /// <inheritdoc />
    public McpServerDefinition Get(string name)
    {
        if (_servers.TryGetValue(name, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"MCP server '{name}' was not found.");
    }
}

/// <summary>
/// 默认的 MCP 客户端管理器。
/// </summary>
/// <param name="catalog">server 配置目录。</param>
public sealed class McpClientManager(IMcpServerCatalog catalog) : IMcpClientManager
{
    private readonly ConcurrentDictionary<string, IMcpSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<IMcpSession> ConnectAsync(string serverName, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(serverName, out var existing))
        {
            return existing;
        }

        var definition = catalog.Get(serverName);
        var session = await ProcessBackedMcpSession.StartAsync(definition, cancellationToken).ConfigureAwait(false);
        _sessions[serverName] = session;
        return session;
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(string serverName)
    {
        if (_sessions.TryRemove(serverName, out var session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IMcpSession> GetConnectedSessions() => _sessions.Values.ToArray();
}

internal sealed class ProcessBackedMcpSession : IMcpSession
{
    private readonly Process _process;

    private ProcessBackedMcpSession(McpServerDefinition definition, Process process)
    {
        ServerName = definition.Name;
        _process = process;
        IsConnected = true;
        Tools = [];
        Resources = [];
        Prompts = [];
    }

    public string ServerName { get; }

    public bool IsConnected { get; private set; }

    public IReadOnlyCollection<McpToolDescriptor> Tools { get; }

    public IReadOnlyCollection<McpResourceDescriptor> Resources { get; }

    public IReadOnlyCollection<McpPromptDescriptor> Prompts { get; }

    public static async Task<IMcpSession> StartAsync(McpServerDefinition definition, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(definition.Command, definition.Arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        foreach (var pair in definition.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start MCP server '{definition.Name}'.", ex);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(definition.TimeoutSeconds));

        try
        {
            if (!string.IsNullOrWhiteSpace(definition.ReadySignal))
            {
                while (!timeout.IsCancellationRequested)
                {
                    var readTask = process.StandardOutput.ReadLineAsync(timeout.Token).AsTask();
                    var line = await readTask.ConfigureAwait(false);
                    if (line is null)
                    {
                        throw new InvalidOperationException($"MCP server '{definition.Name}' exited before ready.");
                    }

                    if (line.Contains(definition.ReadySignal, StringComparison.Ordinal))
                    {
                        return new ProcessBackedMcpSession(definition, process);
                    }
                }

                throw new TimeoutException($"MCP server '{definition.Name}' did not become ready in time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), timeout.Token).ConfigureAwait(false);
            if (process.HasExited)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"MCP server '{definition.Name}' exited early. {stderr}");
            }

            return new ProcessBackedMcpSession(definition, process);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"MCP server '{definition.Name}' did not become ready in time.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        IsConnected = false;
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }

        _process.Dispose();
    }
}

/// <summary>
/// 将 MCP 工具描述适配为本地工具定义。
/// </summary>
/// <param name="descriptor">MCP 工具描述。</param>
public sealed class McpToolAdapter(McpToolDescriptor descriptor) : IToolDefinition
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        descriptor.Name,
        descriptor.Description,
        descriptor.InputSchema,
        null,
        ToolCapability.None);
}

/// <summary>
/// MCP 资源描述的轻量适配器。
/// </summary>
/// <param name="descriptor">MCP 资源描述。</param>
public sealed class McpResourceAdapter(McpResourceDescriptor descriptor)
{
    /// <summary>
    /// 资源名称。
    /// </summary>
    public string Name => descriptor.Name;

    /// <summary>
    /// 资源 URI。
    /// </summary>
    public string Uri => descriptor.Uri;

    /// <summary>
    /// 资源描述。
    /// </summary>
    public string Description => descriptor.Description;
}

/// <summary>
/// MCP prompt 描述的轻量适配器。
/// </summary>
/// <param name="descriptor">MCP prompt 描述。</param>
public sealed class McpPromptAdapter(McpPromptDescriptor descriptor)
{
    /// <summary>
    /// prompt 名称。
    /// </summary>
    public string Name => descriptor.Name;

    /// <summary>
    /// prompt 描述。
    /// </summary>
    public string Description => descriptor.Description;
}
