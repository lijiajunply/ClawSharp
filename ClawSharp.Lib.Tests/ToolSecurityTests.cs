using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Tests;

public sealed class ToolSecurityTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "clawsharp-tests", Guid.NewGuid().ToString("N"));

    public ToolSecurityTests()
    {
        Directory.CreateDirectory(_workspace);
    }

    [Fact]
    public async Task PermissionMerge_IntersectsCapabilitiesAndRestrictions()
    {
        var agent = new ToolPermissionSet(
            ToolCapability.FileRead | ToolCapability.FileWrite,
            ["docs", "src"],
            ["docs"],
            ["echo ok", "pwd"],
            false,
            false,
            60,
            4096);
        var workspace = new ToolPermissionSet(
            ToolCapability.FileRead | ToolCapability.ShellExecute,
            ["docs"],
            ["docs", "tmp"],
            ["pwd"],
            false,
            false,
            30,
            2048);

        var merged = agent.Merge(workspace);

        Assert.Equal(ToolCapability.FileRead, merged.Capabilities);
        Assert.Single(merged.AllowedReadRoots);
        Assert.Single(merged.AllowedCommands);

        var path = Path.Combine(_workspace, "docs", "note.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "hello");

        var tool = new FileReadTool();
        var result = await tool.ExecuteAsync(TestHelpers.CreateContext(_workspace, merged), TestHelpers.Json(new { path = "docs/note.txt" }));
        Assert.Equal(ToolInvocationStatus.Success, result.Status);
    }

    [Fact]
    public async Task FileWrite_DeniedOutsideAllowList()
    {
        var tool = new FileWriteTool();
        var permissions = new ToolPermissionSet(
            ToolCapability.FileWrite,
            [],
            ["allowed"],
            [],
            false,
            false,
            10,
            1024);

        var result = await tool.ExecuteAsync(
            TestHelpers.CreateContext(_workspace, permissions),
            TestHelpers.Json(new { path = "denied/out.txt", content = "test" }));

        Assert.Equal(ToolInvocationStatus.Denied, result.Status);
        Assert.Equal("Write path denied.", result.Error);
    }

    [Fact]
    public async Task ShellRun_RequiresApproval_ForDangerousCommandEvenWithoutGlobalApprovalFlag()
    {
        var tool = new ShellRunTool();
        var permissions = new ToolPermissionSet(
            ToolCapability.ShellExecute,
            [],
            [],
            [],
            false,
            false,
            10,
            1024);

        var result = await tool.ExecuteAsync(
            TestHelpers.CreateContext(_workspace, permissions),
            TestHelpers.Json(new { command = "rm -rf ./tmp" }));

        Assert.Equal(ToolInvocationStatus.ApprovalRequired, result.Status);
        Assert.Equal("critical", result.Payload.GetProperty("riskLevel").GetString());
        Assert.Contains(
            result.Payload.GetProperty("reasons").EnumerateArray().Select(x => x.GetString()),
            reason => reason == "Deletes files recursively and forcefully.");
    }

    [Fact]
    public async Task ShellRun_RequiresApproval_ForAnyCommandWhenApprovalFlagEnabled()
    {
        var tool = new ShellRunTool();
        var permissions = new ToolPermissionSet(
            ToolCapability.ShellExecute,
            [],
            [],
            [],
            true,
            false,
            10,
            1024);

        var result = await tool.ExecuteAsync(
            TestHelpers.CreateContext(_workspace, permissions),
            TestHelpers.Json(new { command = "pwd" }));

        Assert.Equal(ToolInvocationStatus.ApprovalRequired, result.Status);
        Assert.True(result.Payload.GetProperty("requestedByPolicy").GetBoolean());
    }

    [Fact]
    public async Task ShellRun_Succeeds_WhenCommandAlreadyWhitelistedAfterApproval()
    {
        var tool = new ShellRunTool();
        var permissions = new ToolPermissionSet(
            ToolCapability.ShellExecute,
            [],
            [],
            ["pwd"],
            true,
            false,
            10,
            1024);

        var result = await tool.ExecuteAsync(
            TestHelpers.CreateContext(_workspace, permissions),
            TestHelpers.Json(new { command = "pwd" }));

        Assert.Equal(ToolInvocationStatus.Success, result.Status);
        Assert.True(result.Payload.TryGetProperty("stdout", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }
}
