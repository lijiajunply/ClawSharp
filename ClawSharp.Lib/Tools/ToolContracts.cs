using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// 描述工具执行所需的能力位标记。
/// </summary>
[Flags]
public enum ToolCapability
{
    /// <summary>
    /// 不需要任何特殊能力。
    /// </summary>
    None = 0,

    /// <summary>
    /// 允许执行本地 shell 命令。
    /// </summary>
    ShellExecute = 1 << 0,

    /// <summary>
    /// 允许读取文件系统内容。
    /// </summary>
    FileRead = 1 << 1,

    /// <summary>
    /// 允许写入文件系统内容。
    /// </summary>
    FileWrite = 1 << 2,

    /// <summary>
    /// 允许读取系统级信息，例如进程或环境信息。
    /// </summary>
    SystemInspect = 1 << 3,

    /// <summary>
    /// 允许访问网络。
    /// </summary>
    NetworkAccess = 1 << 4,

    /// <summary>
    /// 允许执行版本控制操作（如 Git）。
    /// </summary>
    VersionControl = 1 << 5
}

/// <summary>
/// 将外部字符串声明解析为 <see cref="ToolCapability"/>。
/// </summary>
public static class ToolCapabilityParser
{
    /// <summary>
    /// 尝试解析工具能力字符串。
    /// </summary>
    /// <param name="value">能力声明，例如 <c>file_read</c>。</param>
    /// <param name="capability">解析成功时输出的能力值。</param>
    /// <returns>解析成功时返回 <see langword="true"/>。</returns>
    public static bool TryParse(string value, out ToolCapability capability)
    {
        capability = value.Trim().ToLowerInvariant() switch
        {
            "shell.execute" => ToolCapability.ShellExecute,
            "file_read" => ToolCapability.FileRead,
            "file_write" => ToolCapability.FileWrite,
            "system.inspect" => ToolCapability.SystemInspect,
            "network.access" => ToolCapability.NetworkAccess,
            "version_control" => ToolCapability.VersionControl,
            _ => ToolCapability.None
        };

        return capability != ToolCapability.None;
    }
}

/// <summary>
/// 描述一次工具执行可使用的能力范围与安全限制。
/// </summary>
/// <param name="Capabilities">允许使用的能力位集合。</param>
/// <param name="AllowedReadRoots">允许读取的根目录列表。</param>
/// <param name="AllowedWriteRoots">允许写入的根目录列表。</param>
/// <param name="AllowedCommands">允许执行的命令白名单。</param>
/// <param name="ApprovalRequired">是否要求在执行前进入审批流程。</param>
/// <param name="ReadOnlyFileSystem">是否禁止任何写文件行为。</param>
/// <param name="TimeoutSeconds">工具执行超时上限；为 <see langword="null"/> 时使用工具默认值。</param>
/// <param name="MaxOutputLength">工具输出最大长度；为 <see langword="null"/> 时使用工具默认值。</param>
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
    /// <summary>
    /// 不授予任何能力的空权限集。
    /// </summary>
    public static ToolPermissionSet Empty { get; } =
        new(ToolCapability.None, [], [], []);

    /// <summary>
    /// 将当前权限与另一组权限做最小权限合并。
    /// </summary>
    /// <param name="other">另一组权限，通常来自 workspace 或 agent。</param>
    /// <returns>取交集后的权限集。</returns>
    /// <remarks>
    /// 能力按位与收窄，路径和命令白名单在双方都显式声明时取交集，超时和输出长度取更严格的较小值。
    /// </remarks>
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

/// <summary>
/// workspace 级的默认权限策略。
/// </summary>
public sealed class WorkspacePolicy
{
    /// <summary>
    /// 默认工具权限集合。
    /// </summary>
    public ToolPermissionSet Permissions { get; init; } = ToolPermissionSet.Empty;

    /// <summary>
    /// 全局强制开启的工具列表。
    /// </summary>
    public List<string> MandatoryTools { get; init; } = [];

    /// <summary>
    /// 创建一组偏开发环境的默认权限。
    /// </summary>
    /// <returns>启用常见本地能力的默认策略。</returns>
    public static WorkspacePolicy CreateDefault() =>
        new()
        {
            Permissions = new ToolPermissionSet(
                ToolCapability.ShellExecute | ToolCapability.FileRead | ToolCapability.FileWrite | ToolCapability.SystemInspect | ToolCapability.NetworkAccess | ToolCapability.VersionControl,
                [],
                [],
                [],
                false,
                false,
                60,
                16_384)
        };
}

/// <summary>
/// 描述一个工具的公开元数据和 schema。
/// </summary>
/// <param name="Name">工具名。</param>
/// <param name="Description">工具描述。</param>
/// <param name="InputSchema">输入 JSON Schema；为空表示未声明。</param>
/// <param name="OutputSchema">输出 JSON Schema；为空表示未声明。</param>
/// <param name="Capabilities">执行该工具所需的能力集合。</param>
public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement? InputSchema,
    JsonElement? OutputSchema,
    ToolCapability Capabilities);

/// <summary>
/// 描述一次工具调用的执行上下文。
/// </summary>
/// <param name="WorkspaceRoot">工具运行时可见的 workspace 根目录。</param>
/// <param name="AgentId">发起调用的 agent 标识。</param>
/// <param name="SessionId">所属 session 标识。</param>
/// <param name="TurnId">所属 turn 标识。</param>
/// <param name="MessageId">关联的用户消息标识。</param>
/// <param name="Permissions">本次调用生效的权限集合。</param>
/// <param name="TraceId">用于追踪调用链的 trace 标识。</param>
/// <param name="CancellationToken">调用取消令牌。</param>
/// <param name="Delegation">委派上下文。</param>
public sealed record ToolExecutionContext(
    string WorkspaceRoot,
    string AgentId,
    string SessionId,
    string? TurnId,
    string? MessageId,
    ToolPermissionSet Permissions,
    string TraceId,
    CancellationToken CancellationToken,
    DelegationContext? Delegation = null);

/// <summary>
/// 表示工具调用的最终状态。
/// </summary>
public enum ToolInvocationStatus
{
    /// <summary>
    /// 工具执行成功。
    /// </summary>
    Success,

    /// <summary>
    /// 调用被权限策略直接拒绝。
    /// </summary>
    Denied,

    /// <summary>
    /// 调用命中了审批策略，需要上层继续处理。
    /// </summary>
    ApprovalRequired,

    /// <summary>
    /// 工具自身执行失败。
    /// </summary>
    Failed
}

/// <summary>
/// 表示一次工具调用的结果。
/// </summary>
/// <param name="ToolName">工具名。</param>
/// <param name="Status">调用状态。</param>
/// <param name="Payload">返回给 runtime 或模型的 payload。</param>
/// <param name="Error">失败或拒绝时的可读错误消息。</param>
public sealed record ToolInvocationResult(
    string ToolName,
    ToolInvocationStatus Status,
    JsonElement Payload,
    string? Error = null)
{
    /// <summary>
    /// 创建一个被拒绝的工具调用结果。
    /// </summary>
    /// <param name="toolName">工具名。</param>
    /// <param name="reason">拒绝原因。</param>
    /// <returns>状态为 <see cref="ToolInvocationStatus.Denied"/> 的结果。</returns>
    public static ToolInvocationResult Denied(string toolName, string reason) =>
        new(toolName, ToolInvocationStatus.Denied, JsonSerializer.SerializeToElement(new { error = reason }), reason);

    /// <summary>
    /// 创建一个需要审批的工具调用结果。
    /// </summary>
    /// <param name="toolName">工具名。</param>
    /// <param name="payload">给审批流程或 UI 的预览 payload。</param>
    /// <returns>状态为 <see cref="ToolInvocationStatus.ApprovalRequired"/> 的结果。</returns>
    public static ToolInvocationResult RequiresApproval(string toolName, JsonElement payload) =>
        new(toolName, ToolInvocationStatus.ApprovalRequired, payload);

    /// <summary>
    /// 创建一个成功结果。
    /// </summary>
    /// <param name="toolName">工具名。</param>
    /// <param name="payload">工具输出 payload。</param>
    /// <returns>状态为 <see cref="ToolInvocationStatus.Success"/> 的结果。</returns>
    public static ToolInvocationResult Success(string toolName, JsonElement payload) =>
        new(toolName, ToolInvocationStatus.Success, payload);

    /// <summary>
    /// 创建一个失败结果。
    /// </summary>
    /// <param name="toolName">工具名。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>状态为 <see cref="ToolInvocationStatus.Failed"/> 的结果。</returns>
    public static ToolInvocationResult Failure(string toolName, string error) =>
        new(toolName, ToolInvocationStatus.Failed, JsonSerializer.SerializeToElement(new { error }), error);
}

/// <summary>
/// 公开工具定义元数据的最小契约。
/// </summary>
public interface IToolDefinition
{
    /// <summary>
    /// 工具定义元数据。
    /// </summary>
    ToolDefinition Definition { get; }
}

/// <summary>
/// 可执行工具的契约。
/// </summary>
public interface IToolExecutor : IToolDefinition
{
    /// <summary>
    /// 执行一次工具调用。
    /// </summary>
    /// <param name="context">调用上下文。</param>
    /// <param name="arguments">调用参数 JSON。</param>
    /// <returns>工具调用结果。</returns>
    Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments);
}

/// <summary>
/// 工具注册表与执行入口。
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 获取当前注册的全部工具定义。
    /// </summary>
    /// <returns>工具定义集合。</returns>
    IReadOnlyCollection<ToolDefinition> GetAll();

    /// <summary>
    /// 获取在指定权限集下可暴露给模型的工具定义。
    /// </summary>
    /// <param name="permissions">当前生效权限。</param>
    /// <returns>可授权的工具定义集合。</returns>
    IReadOnlyCollection<ToolDefinition> GetAuthorizedTools(ToolPermissionSet permissions);

    /// <summary>
    /// 按名称执行某个工具。
    /// </summary>
    /// <param name="toolName">工具名。</param>
    /// <param name="context">调用上下文。</param>
    /// <param name="arguments">工具参数 JSON。</param>
    /// <returns>工具调用结果。</returns>
    /// <exception cref="KeyNotFoundException">当工具未注册时抛出。</exception>
    Task<ToolInvocationResult> ExecuteAsync(string toolName, ToolExecutionContext context, JsonElement arguments);
}

/// <summary>
/// 默认的工具注册表实现。
/// </summary>
/// <param name="executors">要注册的静态工具执行器集合。</param>
/// <param name="dynamicProviders">动态工具提供者集合。</param>
/// <param name="permissionScopeManager">工具权限范围管理器。</param>
public sealed class ToolRegistry(
    IEnumerable<IToolExecutor> executors, 
    IEnumerable<IAgentToolProvider> dynamicProviders,
    IPermissionScopeManager permissionScopeManager) : IToolRegistry
{
    private readonly Dictionary<string, IToolExecutor> _staticExecutors =
        executors.ToDictionary(x => x.Definition.Name, StringComparer.OrdinalIgnoreCase);

    private IEnumerable<IToolExecutor> AllExecutors => 
        _staticExecutors.Values.Concat(dynamicProviders.SelectMany(p => p.DiscoverAgentTools()));

    /// <inheritdoc />
    public IReadOnlyCollection<ToolDefinition> GetAll() => 
        AllExecutors.Select(x => x.Definition).ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<ToolDefinition> GetAuthorizedTools(ToolPermissionSet permissions)
    {
        return AllExecutors
            .Select(x => x.Definition)
            .Where(x => (x.Capabilities == ToolCapability.None || permissions.Capabilities.HasFlag(x.Capabilities)) &&
                        permissionScopeManager.CanInvokeTool(x.Name))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<ToolInvocationResult> ExecuteAsync(string toolName, ToolExecutionContext context, JsonElement arguments)
    {
        var executor = AllExecutors.FirstOrDefault(x => x.Definition.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
        if (executor == null)
        {
            throw new KeyNotFoundException($"Tool '{toolName}' was not found.");
        }

        if (!permissionScopeManager.CanInvokeTool(toolName))
        {
            return ToolInvocationResult.Denied(toolName, "Tool is not in the current permission scope.");
        }

        if (executor.Definition.Capabilities != ToolCapability.None &&
            !context.Permissions.Capabilities.HasFlag(executor.Definition.Capabilities))
        {
            return ToolInvocationResult.Denied(toolName, "Capability denied.");
        }

        return await executor.ExecuteAsync(context, arguments).ConfigureAwait(false);
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

    public static bool CommandExists(string command)
    {
        var shell = OperatingSystem.IsWindows() ? "where" : "command -v";
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(shell, command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

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

/// <summary>
/// 本地 shell 命令执行工具。
/// </summary>
public sealed class ShellRunTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "shell_run",
        "Run a shell command in the local workspace.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new { command = new { type = "string", description = "The shell command to run." } },
            required = new[] { "command" }
        }),
        null,
        ToolCapability.ShellExecute);

    /// <inheritdoc />
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

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(shell, shellArgs)
        {
            WorkingDirectory = context.WorkspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
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
                //
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
            ToolInvocationStatus.Success,
            ToolSecurity.Json(new
            {
                exitCode = process.ExitCode,
                stdout,
                stderr
            }));
    }
}

/// <summary>
/// 文件读取工具。
/// </summary>
public sealed class FileReadTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "file_read", 
        "Read a file.", 
        ToolSecurity.Json(new { type = "object", properties = new { path = new { type = "string" } }, required = new[] { "path" } }), 
        null, 
        ToolCapability.FileRead);

    /// <inheritdoc />
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
        return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, content }));
    }
}

/// <summary>
/// 文件写入工具。
/// </summary>
public sealed class FileWriteTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "file_write", 
        "Write a file.", 
        ToolSecurity.Json(new { 
            type = "object", 
            properties = new { 
                path = new { type = "string" },
                content = new { type = "string" }
            }, 
            required = new[] { "path", "content" } 
        }), 
        null, 
        ToolCapability.FileWrite);

    /// <inheritdoc />
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
        return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, bytes = Encoding.UTF8.GetByteCount(content) }));
    }
}

/// <summary>
/// 列出目录下文件系统条目的工具。
/// </summary>
public sealed class FileListTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new("file_list", "List files under a directory.", null, null, ToolCapability.FileRead);

    /// <inheritdoc />
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

        return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, entries })));
    }
}

/// <summary>
/// 返回本机基础系统信息的工具。
/// </summary>
public sealed class SystemInfoTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "system_info",
        "Get local system information.",
        ToolSecurity.Json(new { type = "object", properties = new { } }),
        null,
        ToolCapability.SystemInspect);

    /// <inheritdoc />
    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        return Task.FromResult(ToolInvocationResult.Success(
            Definition.Name,
            ToolSecurity.Json(new
            {
                os = Environment.OSVersion.ToString(),
                machine = Environment.MachineName,
                processors = Environment.ProcessorCount,
                framework = Environment.Version.ToString()
            })));
    }
}

/// <summary>
/// 返回本机进程列表的工具。
/// </summary>
public sealed class SystemProcessesTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new("system_processes", "List local processes.", null, null, ToolCapability.SystemInspect);

    /// <inheritdoc />
    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var processes = Process.GetProcesses()
            .OrderBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select(x => new { x.Id, x.ProcessName })
            .ToArray();

        return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(processes)));
    }
}

/// <summary>
/// 在 workspace 内搜索文本内容的工具。
/// </summary>
public sealed class SearchTextTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new("search_text", "Search text within workspace files.", null, null, ToolCapability.FileRead);

    /// <inheritdoc />
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

        return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(results));
    }
}

/// <summary>
/// 按文件名模式搜索文件的工具。
/// </summary>
public sealed class SearchFilesTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new("search_files", "Search files by pattern.", null, null, ToolCapability.FileRead);

    /// <inheritdoc />
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
        return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(files)));
    }
}

/// <summary>
/// 以树状结构递归列出目录下文件系统条目的工具。
/// </summary>
public sealed class FileTreeTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "file_tree", 
        "List files and directories as a tree structure recursively.", 
        ToolSecurity.Json(new { 
            type = "object", 
            properties = new { 
                path = new { type = "string" },
                depth = new { type = "integer", @default = 3 }
            } 
        }), 
        null, 
        ToolCapability.FileRead);

    /// <inheritdoc />
    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var path = arguments.TryGetProperty("path", out var pathElement) ? pathElement.GetString() ?? "." : ".";
        var depth = arguments.TryGetProperty("depth", out var depthElement) ? depthElement.GetInt32() : 3;
        
        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return Task.FromResult(ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path }));
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(ToolInvocationResult.Failure(Definition.Name, "Directory not found."));
        }

        var sb = new StringBuilder();
        sb.AppendLine(Path.GetFileName(fullPath) + "/");
        BuildTree(new DirectoryInfo(fullPath), "", true, sb, 0, depth);

        return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, tree = sb.ToString() })));
    }

    private static void BuildTree(DirectoryInfo dir, string indent, bool last, StringBuilder sb, int currentDepth, int maxDepth)
    {
        if (currentDepth >= maxDepth) return;

        var entries = dir.GetFileSystemInfos()
            .Where(x => !x.Name.StartsWith('.'))
            .OrderBy(x => x is DirectoryInfo ? 0 : 1)
            .ThenBy(x => x.Name)
            .ToArray();

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            bool isLast = i == entries.Length - 1;
            sb.Append(indent);
            sb.Append(isLast ? "└── " : "├── ");
            sb.Append(entry.Name);
            if (entry is DirectoryInfo) sb.Append("/");
            sb.AppendLine();

            if (entry is DirectoryInfo subDir)
            {
                BuildTree(subDir, indent + (isLast ? "    " : "│   "), isLast, sb, currentDepth + 1, maxDepth);
            }
        }
    }
}
