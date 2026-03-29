using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Tools;

[Flags]
public enum ToolCapability
{
    None = 0,
    ShellExecute = 1 << 0,
    FileRead = 1 << 1,
    FileWrite = 1 << 2,
    SystemInspect = 1 << 3,
    NetworkAccess = 1 << 4
}

public static class ToolCapabilityParser
{
    public static bool TryParse(string value, out ToolCapability capability)
    {
        capability = value.Trim().ToLowerInvariant() switch
        {
            "shell.execute" => ToolCapability.ShellExecute,
            "file.read" => ToolCapability.FileRead,
            "file.write" => ToolCapability.FileWrite,
            "system.inspect" => ToolCapability.SystemInspect,
            "network.access" => ToolCapability.NetworkAccess,
            _ => ToolCapability.None
        };

        return capability != ToolCapability.None;
    }
}

public sealed record ToolPermissionSet(
    ToolCapability Capabilities,
    IReadOnlyCollection<string> AllowedReadRoots,
    IReadOnlyCollection<string> AllowedWriteRoots,
    IReadOnlyCollection<string> AllowedCommands,
    bool ApprovalRequired = false,
    bool ReadOnlyFileSystem = false,
    int? TimeoutSeconds = null,
    int? MaxOutputLength = null)
{
    public static ToolPermissionSet Empty { get; } =
        new(ToolCapability.None, [], [], []);

    public ToolPermissionSet Merge(ToolPermissionSet other)
    {
        return new ToolPermissionSet(
            Capabilities & other.Capabilities,
            IntersectOrUseExplicit(AllowedReadRoots, other.AllowedReadRoots),
            IntersectOrUseExplicit(AllowedWriteRoots, other.AllowedWriteRoots),
            IntersectOrUseExplicit(AllowedCommands, other.AllowedCommands),
            ApprovalRequired || other.ApprovalRequired,
            ReadOnlyFileSystem || other.ReadOnlyFileSystem,
            MinNullable(TimeoutSeconds, other.TimeoutSeconds),
            MinNullable(MaxOutputLength, other.MaxOutputLength));
    }

    private static IReadOnlyCollection<string> IntersectOrUseExplicit(
        IReadOnlyCollection<string> left,
        IReadOnlyCollection<string> right)
    {
        if (left.Count == 0)
        {
            return right.ToArray();
        }

        if (right.Count == 0)
        {
            return left.ToArray();
        }

        return left.Intersect(right, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int? MinNullable(int? left, int? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return Math.Min(left.Value, right.Value);
    }
}

public sealed class WorkspacePolicy
{
    public ToolPermissionSet Permissions { get; init; } = ToolPermissionSet.Empty;

    public static WorkspacePolicy CreateDefault() =>
        new()
        {
            Permissions = new ToolPermissionSet(
                ToolCapability.ShellExecute | ToolCapability.FileRead | ToolCapability.FileWrite | ToolCapability.SystemInspect,
                [],
                [],
                [],
                false,
                false,
                60,
                16_384)
        };
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement? InputSchema,
    JsonElement? OutputSchema,
    ToolCapability Capabilities);

public sealed record ToolExecutionContext(
    string WorkspaceRoot,
    string AgentId,
    string SessionId,
    ToolPermissionSet Permissions,
    string TraceId,
    CancellationToken CancellationToken);

public sealed record ToolInvocationResult(
    string ToolName,
    bool ApprovalRequired,
    string? DeniedReason,
    JsonElement Payload)
{
    public static ToolInvocationResult Denied(string toolName, string reason) =>
        new(toolName, false, reason, JsonSerializer.SerializeToElement(new { error = reason }));

    public static ToolInvocationResult RequiresApproval(string toolName, JsonElement payload) =>
        new(toolName, true, null, payload);
}

public interface IToolDefinition
{
    ToolDefinition Definition { get; }
}

public interface IToolExecutor : IToolDefinition
{
    Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments);
}

public interface IToolRegistry
{
    IReadOnlyCollection<ToolDefinition> GetAll();

    IReadOnlyCollection<ToolDefinition> GetAuthorizedTools(ToolPermissionSet permissions);

    Task<ToolInvocationResult> ExecuteAsync(string toolName, ToolExecutionContext context, JsonElement arguments);
}

public sealed class ToolRegistry(IEnumerable<IToolExecutor> executors) : IToolRegistry
{
    private readonly Dictionary<string, IToolExecutor> _executors =
        executors.ToDictionary(x => x.Definition.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ToolDefinition> GetAll() => _executors.Values.Select(x => x.Definition).ToArray();

    public IReadOnlyCollection<ToolDefinition> GetAuthorizedTools(ToolPermissionSet permissions)
    {
        return _executors.Values
            .Select(x => x.Definition)
            .Where(x => x.Capabilities == ToolCapability.None || permissions.Capabilities.HasFlag(x.Capabilities))
            .ToArray();
    }

    public Task<ToolInvocationResult> ExecuteAsync(string toolName, ToolExecutionContext context, JsonElement arguments)
    {
        if (!_executors.TryGetValue(toolName, out var executor))
        {
            throw new KeyNotFoundException($"Tool '{toolName}' was not found.");
        }

        if (executor.Definition.Capabilities != ToolCapability.None &&
            !context.Permissions.Capabilities.HasFlag(executor.Definition.Capabilities))
        {
            return Task.FromResult(ToolInvocationResult.Denied(toolName, "Capability denied."));
        }

        return executor.ExecuteAsync(context, arguments);
    }
}

internal static class ToolSecurity
{
    public static OperationResult EnsurePathAllowed(
        string workspaceRoot,
        string path,
        IReadOnlyCollection<string> allowedRoots,
        bool write)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workspaceRoot, path));
        if (allowedRoots.Count == 0)
        {
            return OperationResult.Success();
        }

        var isAllowed = allowedRoots.Select(root => Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(workspaceRoot, root)))
            .Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));

        return isAllowed
            ? OperationResult.Success()
            : OperationResult.Failure(write ? "Write path denied." : "Read path denied.");
    }

    public static OperationResult EnsureCommandAllowed(string command, IReadOnlyCollection<string> allowedCommands)
    {
        if (allowedCommands.Count == 0)
        {
            return OperationResult.Success();
        }

        var normalized = command.Trim();
        return allowedCommands.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? OperationResult.Success()
            : OperationResult.Failure("Command denied.");
    }

    public static ToolInvocationResult CreateApprovalOrDenied(
        ToolDefinition definition,
        ToolExecutionContext context,
        string reason,
        object? preview = null)
    {
        var payload = JsonSerializer.SerializeToElement(preview ?? new { message = reason });
        return context.Permissions.ApprovalRequired
            ? ToolInvocationResult.RequiresApproval(definition.Name, payload)
            : ToolInvocationResult.Denied(definition.Name, reason);
    }

    public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    public static async Task<string> ReadStandardOutputAsync(Process process, int maxLength, CancellationToken cancellationToken)
    {
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (output.Length <= maxLength)
        {
            return output;
        }

        return output[..maxLength];
    }
}

public sealed class ShellRunTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "shell.run",
        "Run a shell command in the local workspace.",
        null,
        null,
        ToolCapability.ShellExecute);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var command = arguments.GetProperty("command").GetString() ?? string.Empty;
        var commandCheck = ToolSecurity.EnsureCommandAllowed(command, context.Permissions.AllowedCommands);
        if (!commandCheck.IsSuccess)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, commandCheck.Error!, new { command });
        }

        var shell = OperatingSystem.IsWindows() ? "cmd" : "/bin/zsh";
        var shellArgs = OperatingSystem.IsWindows() ? $"/c {command}" : $"-lc \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(shell, shellArgs)
            {
                WorkingDirectory = context.WorkspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var timeoutSeconds = context.Permissions.TimeoutSeconds ?? 60;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
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

            return ToolInvocationResult.Denied(Definition.Name, "Command timed out.");
        }

        var maxLength = context.Permissions.MaxOutputLength ?? 16_384;
        var stdout = await ToolSecurity.ReadStandardOutputAsync(process, maxLength, context.CancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(context.CancellationToken).ConfigureAwait(false);
        if (stderr.Length > maxLength)
        {
            stderr = stderr[..maxLength];
        }

        return new ToolInvocationResult(
            Definition.Name,
            false,
            null,
            ToolSecurity.Json(new
            {
                exitCode = process.ExitCode,
                stdout,
                stderr
            }));
    }
}

public sealed class FileReadTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("file.read", "Read a file.", null, null, ToolCapability.FileRead);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var path = arguments.GetProperty("path").GetString() ?? string.Empty;
        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path });
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        var content = await File.ReadAllTextAsync(fullPath, context.CancellationToken).ConfigureAwait(false);
        return new ToolInvocationResult(Definition.Name, false, null, ToolSecurity.Json(new { path = fullPath, content }));
    }
}

public sealed class FileWriteTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("file.write", "Write a file.", null, null, ToolCapability.FileWrite);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        if (context.Permissions.ReadOnlyFileSystem)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, "File system is read-only.");
        }

        var path = arguments.GetProperty("path").GetString() ?? string.Empty;
        var content = arguments.GetProperty("content").GetString() ?? string.Empty;
        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedWriteRoots, write: true);
        if (!check.IsSuccess)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path });
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, context.CancellationToken).ConfigureAwait(false);
        return new ToolInvocationResult(Definition.Name, false, null, ToolSecurity.Json(new { path = fullPath, bytes = Encoding.UTF8.GetByteCount(content) }));
    }
}

public sealed class FileListTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("file.list", "List files under a directory.", null, null, ToolCapability.FileRead);

    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var path = arguments.TryGetProperty("path", out var pathElement) ? pathElement.GetString() ?? "." : ".";
        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return Task.FromResult(ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path }));
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        var entries = Directory.EnumerateFileSystemEntries(fullPath)
            .Select(Path.GetFileName)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(new ToolInvocationResult(Definition.Name, false, null, ToolSecurity.Json(new { path = fullPath, entries })));
    }
}

public sealed class SystemInfoTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("system.info", "Get local system information.", null, null, ToolCapability.SystemInspect);

    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        return Task.FromResult(new ToolInvocationResult(
            Definition.Name,
            false,
            null,
            ToolSecurity.Json(new
            {
                os = Environment.OSVersion.ToString(),
                machine = Environment.MachineName,
                processors = Environment.ProcessorCount,
                framework = Environment.Version.ToString()
            })));
    }
}

public sealed class SystemProcessesTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("system.processes", "List local processes.", null, null, ToolCapability.SystemInspect);

    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var processes = Process.GetProcesses()
            .OrderBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select(x => new { x.Id, x.ProcessName })
            .ToArray();

        return Task.FromResult(new ToolInvocationResult(Definition.Name, false, null, ToolSecurity.Json(processes)));
    }
}

public sealed class SearchTextTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("search.text", "Search text within workspace files.", null, null, ToolCapability.FileRead);

    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var needle = arguments.GetProperty("query").GetString() ?? string.Empty;
        var path = arguments.TryGetProperty("path", out var pathElement) ? pathElement.GetString() ?? "." : ".";
        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path });
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        var results = new List<object>();

        foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
        {
            var content = await File.ReadAllTextAsync(file, context.CancellationToken).ConfigureAwait(false);
            if (content.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new { path = file });
            }
        }

        return new ToolInvocationResult(Definition.Name, false, null, ToolSecurity.Json(results));
    }
}

public sealed class SearchFilesTool : IToolExecutor
{
    public ToolDefinition Definition { get; } = new("search.files", "Search files by pattern.", null, null, ToolCapability.FileRead);

    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var pattern = arguments.GetProperty("pattern").GetString() ?? "*";
        var path = arguments.TryGetProperty("path", out var pathElement) ? pathElement.GetString() ?? "." : ".";
        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return Task.FromResult(ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path }));
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        var files = Directory.EnumerateFiles(fullPath, pattern, SearchOption.AllDirectories).ToArray();
        return Task.FromResult(new ToolInvocationResult(Definition.Name, false, null, ToolSecurity.Json(files)));
    }
}
