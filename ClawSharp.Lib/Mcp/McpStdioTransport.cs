using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// Implements MCP transport over standard I/O.
/// </summary>
public class McpStdioTransport : IAsyncDisposable
{
    private readonly McpServerConfig _config;
    private Process? _process;
    private readonly CancellationTokenSource _cts = new();
    private Task? _readTask;

    /// <summary>
    /// 当收到完整的 MCP 消息时触发。
    /// </summary>
    public event Action<McpMessage>? MessageReceived;

    /// <summary>
    /// 当标准错误流收到输出时触发。
    /// </summary>
    public event Action<string>? ErrorReceived;

    /// <summary>
    /// 触发 <see cref="MessageReceived"/> 事件。
    /// </summary>
    /// <param name="message">收到的消息。</param>
    protected void OnMessageReceived(McpMessage message) => MessageReceived?.Invoke(message);

    /// <summary>
    /// 触发 <see cref="ErrorReceived"/> 事件。
    /// </summary>
    /// <param name="error">错误消息。</param>
    protected void OnErrorReceived(string error) => ErrorReceived?.Invoke(error);

    /// <summary>
    /// 初始化 <see cref="McpStdioTransport"/> 类的新实例。
    /// </summary>
    /// <param name="config">服务器启动配置。</param>
    public McpStdioTransport(McpServerConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 异步启动 MCP 服务器进程。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步启动操作的任务。</returns>
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _config.Command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        foreach (var arg in _config.Args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (_config.Env != null)
        {
            foreach (var (key, value) in _config.Env)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        _process = new Process { StartInfo = startInfo };
        _process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) OnErrorReceived(e.Data);
        };

        if (!_process.Start())
        {
            throw new Exception($"Failed to start MCP server: {_config.Command}");
        }

        _process.BeginErrorReadLine();
        _readTask = Task.Run(ReadLoopAsync, _cts.Token);

        await Task.CompletedTask;
    }

    private async Task ReadLoopAsync()
    {
        using var reader = _process!.StandardOutput;
        while (!_cts.Token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                // Attempt to deserialize as Response first, then Request
                // In a more robust implementation, we'd peek at fields
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("method", out _))
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line);
                    if (request != null) OnMessageReceived(request);
                }
                else
                {
                    var response = JsonSerializer.Deserialize<McpResponse>(line);
                    if (response != null) OnMessageReceived(response);
                }
            }
            catch (JsonException ex)
            {
                OnErrorReceived($"JSON parse error: {ex.Message} in line: {line}");
            }
        }
    }

    /// <summary>
    /// 异步向服务器发送消息。
    /// </summary>
    /// <param name="message">要发送的消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    public virtual async Task SendAsync(McpMessage message, CancellationToken cancellationToken = default)
    {
        if (_process == null || _process.HasExited)
        {
            throw new InvalidOperationException("Transport is not connected.");
        }

        var json = JsonSerializer.Serialize((object)message);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步释放资源并关闭服务器进程。
    /// </summary>
    /// <returns>表示异步释放操作的任务。</returns>
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_process != null)
        {
            if (!_process.HasExited)
            {
                _process.Kill();
            }
            _process.Dispose();
        }

        if (_readTask != null)
        {
            try { await _readTask; } catch { /* ignore */ }
        }
        _cts.Dispose();
    }
}
