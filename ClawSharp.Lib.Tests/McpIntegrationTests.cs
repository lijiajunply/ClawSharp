using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Tools;
using Xunit;

namespace ClawSharp.Lib.Tests;

public class McpIntegrationTests
{
    [Fact]
    public async Task McpService_ShouldListAndCallTools()
    {
        // This test requires a real MCP server or a very good mock.
        // Since we don't want to rely on external node/python in CI,
        // we'll mock the transport to simulate a server.
        
        var config = new McpServerConfig
        {
            Command = "mock",
            Args = []
        };

        // We'll manually test the proxy and service logic with a mock client
        // because the StdioTransport actually starts a process.
        // To properly test this without processes, we'd need IMcpTransport interface.
        // For now, let's verify the McpToolProxy logic.

        var mockTransport = new MockTransport();
        var client = new McpClient(mockTransport);

        var proxy = new McpToolProxy(client, "myserver", "echo", "Echoes input", null);
        
        var context = new ToolExecutionContext(
            Directory.GetCurrentDirectory(),
            "agent-1",
            "session-1",
            null,
            null,
            ToolPermissionSet.Empty,
            "trace-1",
            CancellationToken.None);

        var args = JsonSerializer.SerializeToElement(new { message = "hello" });

        // Setup mock response
        mockTransport.EnqueueResponse(new McpResponse
        {
            Id = JsonDocument.Parse("\"1\"").RootElement,
            Result = JsonSerializer.SerializeToElement(new { content = "hello" })
        });

        var result = await proxy.ExecuteAsync(context, args);

        Assert.Equal(ToolInvocationStatus.Success, result.Status);
        Assert.Equal("hello", result.Payload.GetProperty("content").GetString());
    }

    private class MockTransport : McpStdioTransport
    {
        private readonly Queue<McpResponse> _responses = new();

        public MockTransport() : base(new McpServerConfig { Command = "mock" }) { }

        public void EnqueueResponse(McpResponse response) => _responses.Enqueue(response);

        public override Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task SendAsync(McpMessage message, CancellationToken ct)
        {
            if (message is McpRequest request && request.Method == "tools/call")
            {
                if (_responses.TryDequeue(out var resp))
                {
                    var responseWithCorrectId = resp with { Id = request.Id };
                    _ = Task.Run(() => OnMessageReceived(responseWithCorrectId));
                }
            }
            return Task.CompletedTask;
        }
    }
}
