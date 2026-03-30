using System.Collections.Concurrent;
using System.Text.Json;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// Client for interacting with an MCP server.
/// </summary>
public sealed class McpClient : IAsyncDisposable
{
    private readonly McpStdioTransport _transport;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<McpResponse>> _pendingRequests = new();
    private int _requestIdCounter;

    /// <summary>
    /// 初始化 <see cref="McpClient"/> 类的新实例。
    /// </summary>
    /// <param name="transport">用于通信的传输层。</param>
    public McpClient(McpStdioTransport transport)
    {
        _transport = transport;
        _transport.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(McpMessage message)
    {
        if (message is McpResponse response && response.Id != null)
        {
            var idStr = response.Id.Value.ToString();
            if (_pendingRequests.TryRemove(idStr, out var tcs))
            {
                tcs.SetResult(response);
            }
        }
        // Notifications or server-initiated requests would be handled here
    }

    /// <summary>
    /// 初始化与 MCP 服务器的会话。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步操作的任务。</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var response = await CallAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "ClawSharp", version = "1.0.0" }
        }, cancellationToken).ConfigureAwait(false);

        if (response.Error != null)
        {
            throw new Exception($"MCP initialization failed: {response.Error.Message}");
        }

        // Notify initialized
        await _transport.SendAsync(new McpRequest
        {
            Method = "notifications/initialized"
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 向服务器发送请求并等待响应。
    /// </summary>
    /// <param name="method">RPC 方法名。</param>
    /// <param name="params">方法参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>服务器响应。</returns>
    public async Task<McpResponse> CallAsync(string method, object? @params = null, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _requestIdCounter).ToString();
        var tcs = new TaskCompletionSource<McpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var idElement = JsonDocument.Parse($"\"{id}\"");
        var request = new McpRequest
        {
            Id = idElement.RootElement.Clone(),
            Method = method,
            Params = @params
        };

        _pendingRequests[id] = tcs;

        try
        {
            await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            _pendingRequests.TryRemove(id, out _);
            throw;
        }
    }

    /// <summary>
    /// 异步释放资源并关闭传输。
    /// </summary>
    /// <returns>表示异步释放操作的任务。</returns>
    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
