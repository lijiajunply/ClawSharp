using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Mcp;

public sealed class McpServerDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int TimeoutSeconds { get; set; } = 10;

    public string? ReadySignal { get; set; }

    public ToolCapability Capabilities { get; set; } = ToolCapability.None;
}

public sealed record McpToolDescriptor(string Name, string Description, JsonElement? InputSchema = null);

public sealed record McpResourceDescriptor(string Name, string Uri, string Description);

public sealed record McpPromptDescriptor(string Name, string Description);

public interface IMcpServerCatalog
{
    IReadOnlyCollection<McpServerDefinition> GetAll();

    McpServerDefinition Get(string name);
}

public interface IMcpSession : IAsyncDisposable
{
    string ServerName { get; }

    bool IsConnected { get; }

    IReadOnlyCollection<McpToolDescriptor> Tools { get; }

    IReadOnlyCollection<McpResourceDescriptor> Resources { get; }

    IReadOnlyCollection<McpPromptDescriptor> Prompts { get; }
}

public interface IMcpClientManager
{
    Task<IMcpSession> ConnectAsync(string serverName, CancellationToken cancellationToken = default);

    Task DisconnectAsync(string serverName);

    IReadOnlyCollection<IMcpSession> GetConnectedSessions();
}

public interface IMcpHost
{
}

public interface IMcpExposedToolProvider
{
    IReadOnlyCollection<ToolDefinition> GetTools();
}

public interface IMcpExposedResourceProvider
{
    Task<string> ReadAsync(string uri, CancellationToken cancellationToken = default);
}

public sealed class McpServerCatalog(ClawOptions options) : IMcpServerCatalog
{
    private readonly Dictionary<string, McpServerDefinition> _servers =
        options.Mcp.Servers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<McpServerDefinition> GetAll() => _servers.Values.ToArray();

    public McpServerDefinition Get(string name)
    {
        if (_servers.TryGetValue(name, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"MCP server '{name}' was not found.");
    }
}

public sealed class McpClientManager(IMcpServerCatalog catalog) : IMcpClientManager
{
    private readonly ConcurrentDictionary<string, IMcpSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

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

    public async Task DisconnectAsync(string serverName)
    {
        if (_sessions.TryRemove(serverName, out var session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

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

public sealed class McpToolAdapter(McpToolDescriptor descriptor) : IToolDefinition
{
    public ToolDefinition Definition { get; } = new(
        descriptor.Name,
        descriptor.Description,
        descriptor.InputSchema,
        null,
        ToolCapability.None);
}

public sealed class McpResourceAdapter(McpResourceDescriptor descriptor)
{
    public string Name => descriptor.Name;

    public string Uri => descriptor.Uri;

    public string Description => descriptor.Description;
}

public sealed class McpPromptAdapter(McpPromptDescriptor descriptor)
{
    public string Name => descriptor.Name;

    public string Description => descriptor.Description;
}
