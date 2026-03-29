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
        Assert.Null(result.DeniedReason);
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

        Assert.Equal("Write path denied.", result.DeniedReason);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }
}
