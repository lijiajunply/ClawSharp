using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Tools;

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

    [Fact]
    public async Task McpClientManager_ReusesReleasedSession()
    {
        var options = new ClawOptions();
        options.Mcp.Servers.Add(new McpServerDefinition { Name = "mock", Command = "mock" });
        var createdSessions = 0;
        var manager = new McpClientManager(
            new McpServerCatalog(options),
            options,
            (_, _) =>
            {
                createdSessions++;
                return Task.FromResult<IMcpSession>(new FakeMcpSession("mock"));
            });

        var first = await manager.ConnectAsync("mock");
        await manager.ReleaseAsync("mock");
        var second = await manager.ConnectAsync("mock");

        Assert.Same(first, second);
        Assert.Equal(1, createdSessions);
    }

    [Fact]
    public async Task McpClientManager_CleansUpIdleSessionsAfterTtl()
    {
        var options = new ClawOptions();
        options.Mcp.Pool.IdleTtlSeconds = 1;
        options.Mcp.Servers.Add(new McpServerDefinition { Name = "mock", Command = "mock" });
        var session = new FakeMcpSession("mock");
        var manager = new McpClientManager(
            new McpServerCatalog(options),
            options,
            (_, _) => Task.FromResult<IMcpSession>(session));

        _ = await manager.ConnectAsync("mock");
        await manager.ReleaseAsync("mock");
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        await manager.CleanupIdleAsync();

        Assert.Empty(manager.GetConnectedSessions());
        Assert.True(session.DisposeCount > 0);
    }

    [Fact]
    public async Task McpClientManager_RecreatesFaultedSession()
    {
        var options = new ClawOptions();
        options.Mcp.Servers.Add(new McpServerDefinition { Name = "mock", Command = "mock" });
        var createdSessions = 0;
        var sessions = new Queue<FakeMcpSession>([
            new FakeMcpSession("mock"),
            new FakeMcpSession("mock")
        ]);
        var manager = new McpClientManager(
            new McpServerCatalog(options),
            options,
            (_, _) =>
            {
                createdSessions++;
                return Task.FromResult<IMcpSession>(sessions.Dequeue());
            });

        var first = (FakeMcpSession)await manager.ConnectAsync("mock");
        await manager.ReleaseAsync("mock");
        first.IsAlive = false;

        var second = await manager.ConnectAsync("mock");

        Assert.NotSame(first, second);
        Assert.Equal(2, createdSessions);
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

    private sealed class FakeMcpSession(string serverName) : IMcpSession
    {
        public string ServerName { get; } = serverName;

        public bool IsConnected => IsAlive;

        public bool IsAlive { get; set; } = true;

        public int DisposeCount { get; private set; }

        public IReadOnlyCollection<McpToolDescriptor> Tools { get; } = [];

        public IReadOnlyCollection<McpResourceDescriptor> Resources { get; } = [];

        public IReadOnlyCollection<McpPromptDescriptor> Prompts { get; } = [];

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            IsAlive = false;
            return ValueTask.CompletedTask;
        }
    }
}
