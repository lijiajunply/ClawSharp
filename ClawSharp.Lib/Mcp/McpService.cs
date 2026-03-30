using System.Collections.Concurrent;
using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// Service for managing MCP server lifecycles and tool registration.
/// </summary>
public sealed class McpService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = new();

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
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
                _clients[name] = client;
            }
            catch (Exception ex)
            {
                // In a real app, log error and continue
                Console.WriteLine($"[MCP] Failed to start server '{name}': {ex.Message}");
                await client.DisposeAsync();
            }
        }
    }

    public async Task<IReadOnlyList<IToolExecutor>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<IToolExecutor>();
        foreach (var (serverName, client) in _clients)
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

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }
        _clients.Clear();
    }
}
