using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Tests;

public sealed class McpTests
{
    [Fact]
    public async Task McpClientManager_ThrowsForMissingCommand()
    {
        var options = new ClawOptions
        {
            Mcp = new McpOptions
            {
                Servers =
                [
                    new McpServerDefinition { Name = "missing", Command = "/definitely/missing/command" }
                ]
            }
        };

        var manager = new McpClientManager(new McpServerCatalog(options));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConnectAsync("missing"));
    }

    [Fact]
    public async Task McpClientManager_TimesOutWhenReadySignalNeverArrives()
    {
        var shell = OperatingSystem.IsWindows() ? "cmd" : "/bin/zsh";
        var args = OperatingSystem.IsWindows()
            ? "/c ping 127.0.0.1 -n 2 >nul"
            : "-lc \"sleep 1\"";

        var options = new ClawOptions
        {
            Mcp = new McpOptions
            {
                Servers =
                [
                    new McpServerDefinition
                    {
                        Name = "slow",
                        Command = shell,
                        Arguments = args,
                        ReadySignal = "READY",
                        TimeoutSeconds = 1,
                        Capabilities = ToolCapability.FileRead
                    }
                ]
            }
        };

        var manager = new McpClientManager(new McpServerCatalog(options));
        await Assert.ThrowsAsync<TimeoutException>(() => manager.ConnectAsync("slow"));
    }
}
