using System.Text.Json;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Tests;

internal static class TestHelpers
{
    public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    public static ToolExecutionContext CreateContext(
        string workspaceRoot,
        ToolPermissionSet? permissions = null) =>
        new(
            workspaceRoot,
            "agent.test",
            "session.test",
            permissions ?? new ToolPermissionSet(
                ToolCapability.ShellExecute | ToolCapability.FileRead | ToolCapability.FileWrite | ToolCapability.SystemInspect,
                [],
                [],
                [],
                false,
                false,
                5,
                4096),
            "trace.test",
            CancellationToken.None);
}
