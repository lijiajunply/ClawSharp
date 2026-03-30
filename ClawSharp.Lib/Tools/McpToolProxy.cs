using System.Text.Json;
using ClawSharp.Lib.Mcp;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// Wraps an MCP tool as a standard ClawSharp IToolExecutor.
/// </summary>
public sealed class McpToolProxy : IToolExecutor
{
    private readonly McpClient _client;
    private readonly string _serverName;

    public ToolDefinition Definition { get; }

    public McpToolProxy(McpClient client, string serverName, string name, string description, JsonElement? inputSchema)
    {
        _client = client;
        _serverName = serverName;
        // MCP tools are considered external and might require network or system access depending on the server,
        // but since we don't know for sure, we can mark them with None or a special bit if we had one.
        // For now, we'll use None and let the MCP server handle its own security, 
        // while ClawSharp's ToolRegistry handles the capability check.
        Definition = new ToolDefinition(
            $"{serverName}_{name}",
            description,
            inputSchema,
            null,
            ToolCapability.None);
    }

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        try
        {
            var response = await _client.CallAsync("tools/call", new
            {
                name = Definition.Name.Substring(_serverName.Length + 1), // Strip server prefix
                arguments = arguments
            }, context.CancellationToken).ConfigureAwait(false);

            if (response.Error != null)
            {
                return ToolInvocationResult.Failure(Definition.Name, response.Error.Message);
            }

            return ToolInvocationResult.Success(Definition.Name, (JsonElement)response.Result!);
        }
        catch (Exception ex)
        {
            return ToolInvocationResult.Failure(Definition.Name, ex.Message);
        }
    }
}
