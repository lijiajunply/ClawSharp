using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// Service for managing MCP server lifecycles and tool registration.
/// </summary>
public sealed class McpService : IAsyncDisposable
{
    private readonly ClawOptions _options;
    private readonly McpClientManager _manager;
    private readonly List<McpClient> _prewarmedClients = [];
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private PeriodicTimer? _cleanupTimer;
    private Task? _cleanupLoop;
    private bool _started;

    /// <summary>
    /// 初始化 <see cref="McpService"/> 类的新实例。
    /// </summary>
    public McpService(ClawOptions options, McpClientManager manager)
    {
        _options = options;
        _manager = manager;
    }

    /// <summary>
    /// 根据配置文件启动所有已注册的 MCP 服务器。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步启动操作的任务。</returns>
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCleanupLoopStartedAsync(cancellationToken).ConfigureAwait(false);

        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp", "mcp.json");
        if (!File.Exists(configPath)) return;

        var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<McpConfiguration>(json);
        if (config == null) return;

        foreach (var (name, serverConfig) in config.McpServers)
        {
            var transport = new McpStdioTransport(serverConfig);
            var client = new McpClient(transport);
            try
            {
                await transport.StartAsync(cancellationToken).ConfigureAwait(false);
                await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _prewarmedClients.Add(client);
            }
            catch (Exception ex)
            {
                // In a real app, log error and continue
                Console.WriteLine($"[MCP] Failed to start server '{name}': {ex.Message}");
                await client.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// 从所有已连接的 MCP 服务器中获取可用工具列表。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具执行器集合。</returns>
    public async Task<IReadOnlyList<IToolExecutor>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<IToolExecutor>();
        var clients = _prewarmedClients
            .Select(client => (serverName: "prewarmed", client))
            .ToList();

        foreach (var session in _manager.GetConnectedSessions())
        {
            if (session is not McpClientBackedSession backedSession)
            {
                continue;
            }

            clients.Add((session.ServerName, backedSession.Client));
        }

        foreach (var (serverName, client) in clients)
        {
            var response = await client.CallAsync("tools/list", null, cancellationToken).ConfigureAwait(false);
            if (response.Error != null || response.Result == null) continue;

            var result = (JsonElement)response.Result;
            if (result.TryGetProperty("tools", out var toolsArray))
            {
                foreach (var tool in toolsArray.EnumerateArray())
                {
                    var name = tool.GetProperty("name").GetString()!;
                    var desc = tool.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var schema = tool.TryGetProperty("inputSchema", out var s) ? s.Clone() : (JsonElement?)null;
                    tools.Add(new McpToolProxy(client, serverName, name, desc, schema));
                }
            }
        }
        return tools;
    }

    /// <summary>
    /// 强制清理 MCP 连接池并重新加载配置。
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        foreach (var client in _prewarmedClients)
        {
            await client.DisposeAsync();
        }
        _prewarmedClients.Clear();
        await _manager.DisconnectAllAsync(cancellationToken).ConfigureAwait(false);
        await StartAllAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureCleanupLoopStartedAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            var period = TimeSpan.FromSeconds(Math.Max(1, Math.Min(_options.Mcp.Pool.IdleTtlSeconds, 60)));
            _cleanupTimer = new PeriodicTimer(period);
            _cleanupLoop = Task.Run(async () =>
            {
                try
                {
                    while (_cleanupTimer is not null && await _cleanupTimer.WaitForNextTickAsync().ConfigureAwait(false))
                    {
                        await _manager.CleanupIdleAsync().ConfigureAwait(false);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }, CancellationToken.None);
            _started = true;
        }
        finally
        {
            _startGate.Release();
        }
    }

    /// <summary>
    /// 异步释放所有 MCP 客户端和传输资源。
    /// </summary>
    /// <returns>表示异步释放操作的任务。</returns>
    public async ValueTask DisposeAsync()
    {
        if (_cleanupTimer is not null)
        {
            _cleanupTimer.Dispose();
        }

        if (_cleanupLoop is not null)
        {
            try
            {
                await _cleanupLoop.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        foreach (var client in _prewarmedClients)
        {
            await client.DisposeAsync();
        }

        _prewarmedClients.Clear();
        await _manager.DisconnectAllAsync().ConfigureAwait(false);
        _startGate.Dispose();
    }
}

internal sealed class McpClientBackedSession(string serverName, McpClient client) : IMcpSession
{
    public string ServerName { get; } = serverName;

    public bool IsConnected => true;

    public IReadOnlyCollection<McpToolDescriptor> Tools { get; } = [];

    public IReadOnlyCollection<McpResourceDescriptor> Resources { get; } = [];

    public IReadOnlyCollection<McpPromptDescriptor> Prompts { get; } = [];

    public McpClient Client { get; } = client;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
