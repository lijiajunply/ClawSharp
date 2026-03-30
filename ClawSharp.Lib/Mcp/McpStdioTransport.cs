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

    public event Action<McpMessage>? MessageReceived;
    public event Action<string>? ErrorReceived;

    protected void OnMessageReceived(McpMessage message) => MessageReceived?.Invoke(message);
    protected void OnErrorReceived(string error) => ErrorReceived?.Invoke(error);

    public McpStdioTransport(McpServerConfig config)
    {
        _config = config;
    }

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
        while (!_cts.Token.IsCancellationRequested && !reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
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
