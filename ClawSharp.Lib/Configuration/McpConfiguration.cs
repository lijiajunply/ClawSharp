using System.Text.Json.Serialization;

namespace ClawSharp.Lib.Configuration;

/// <summary>
/// Root configuration for MCP (Model Context Protocol).
/// </summary>
public sealed class McpConfiguration
{
    /// <summary>
    /// Map of MCP server configurations.
    /// </summary>
    [JsonPropertyName("mcpServers")]
    public Dictionary<string, McpServerConfig> McpServers { get; set; } = new();
}

/// <summary>
/// Configuration for a single MCP server.
/// </summary>
public sealed class McpServerConfig
{
    /// <summary>
    /// The command to execute (e.g., "node", "python", or absolute path).
    /// </summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Arguments to pass to the command.
    /// </summary>
    [JsonPropertyName("args")]
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional environment variables for the server process.
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }
}
