using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;
using System.Text.Json;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 负责解析 Agent 最终生效权限的核心组件。
/// </summary>
public interface IPermissionResolver
{
    /// <summary>
    /// 根据 Agent 定义和工作空间策略计算最终生效的权限集合。
    /// </summary>
    ToolPermissionSet Resolve(AgentDefinition agent, WorkspacePolicy policy);
}

/// <summary>
/// 负责与用户进行即时权限确认的 UI 抽象。
/// </summary>
public interface IPermissionUI
{
    /// <summary>
    /// 向用户发起即时权限提升请求。
    /// </summary>
    /// <param name="agentId">发起请求的 Agent。</param>
    /// <param name="capability">请求的能力位。</param>
    /// <param name="toolName">关联的工具名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户是否批准。</returns>
    Task<bool> RequestCapabilityAsync(string agentId, ToolCapability capability, string toolName, CancellationToken cancellationToken = default);
}

/// <summary>
/// 运行时视角下的 session 快照。
/// </summary>
/// <param name="Record">底层 session 记录。</param>
/// <param name="EffectivePermissions">当前已解析出的有效权限；未解析时为 <see langword="null"/>。</param>
/// <param name="WorkerHandle">关联的 worker 句柄；未启动 worker 时为 <see langword="null"/>。</param>
public sealed record RuntimeSession(SessionRecord Record, ToolPermissionSet? EffectivePermissions, AgentProcessHandle? WorkerHandle);

/// <summary>
/// 请求在某个 ThreadSpace 中启动 session。
/// </summary>
/// <param name="AgentId">agent 标识。</param>
/// <param name="ThreadSpaceId">目标 ThreadSpace 标识。</param>
public sealed record StartSessionRequest(string AgentId, ThreadSpaceId ThreadSpaceId);

/// <summary>
/// 请求为某个 session 准备 agent 的输入参数。
/// </summary>
/// <param name="AgentId">agent 标识。</param>
/// <param name="SessionId">session 标识。</param>
public sealed record AgentLaunchRequest(string AgentId, SessionId SessionId);

/// <summary>
/// 运行时在真正启动 worker 前解析出的完整 agent 执行计划。
/// </summary>
/// <param name="Session">session 快照。</param>
/// <param name="Agent">agent 定义。</param>
/// <param name="Skills">需装配的 skill 列表。</param>
/// <param name="Tools">本次暴露给 agent 的工具列表。</param>
/// <param name="McpServers">需要连接的 MCP server 列表。</param>
/// <param name="ProviderTarget">已解析的 provider 目标。</param>
/// <param name="History">当前 session 的历史消息。</param>
/// <param name="CacheHit">本次计划是否命中缓存。</param>
/// <param name="DefinitionHash">参与缓存判定的定义哈希。</param>
public sealed record AgentLaunchPlan(
    RuntimeSession Session,
    AgentDefinition Agent,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<ToolDefinition> Tools,
    IReadOnlyList<McpServerDefinition> McpServers,
    ResolvedModelTarget ProviderTarget,
    IReadOnlyList<PromptMessage> History,
    bool CacheHit = false,
    string? DefinitionHash = null);

/// <summary>
/// 表示缓存中的静态 agent 启动计划。
/// </summary>
public sealed class AgentLaunchPlanCacheEntry
{
    /// <summary>
    /// Agent 标识。
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// 当前定义的稳定哈希。
    /// </summary>
    public required string DefinitionHash { get; init; }

    /// <summary>
    /// 缓存的 Agent 定义。
    /// </summary>
    public required AgentDefinition Agent { get; init; }

    /// <summary>
    /// 解析出的 Skill 列表。
    /// </summary>
    public required IReadOnlyList<SkillDefinition> Skills { get; init; }

    /// <summary>
    /// 授权后的工具列表。
    /// </summary>
    public required IReadOnlyList<ToolDefinition> Tools { get; init; }

    /// <summary>
    /// 运行前需要连接的 MCP server 列表。
    /// </summary>
    public required IReadOnlyList<McpServerDefinition> McpServers { get; init; }

    /// <summary>
    /// 解析出的模型目标。
    /// </summary>
    public required ResolvedModelTarget ProviderTarget { get; init; }

    /// <summary>
    /// 最后一次命中时间。
    /// </summary>
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 表示一次 turn 运行完成后的摘要结果。
/// </summary>
/// <param name="SessionId">所属 session。</param>
/// <param name="TurnId">本次执行的 turn 标识。</param>
/// <param name="AssistantMessage">assistant 最终回复文本。</param>
/// <param name="Status">turn 完成后的 session 状态。</param>
/// <param name="ToolCallCount">本次 turn 中发生的工具调用次数。</param>
/// <param name="Performance">本次 turn 的性能指标摘要。</param>
public sealed record RunTurnResult(
    SessionId SessionId,
    TurnId TurnId,
    string AssistantMessage,
    SessionStatus Status,
    int ToolCallCount,
    PerformanceMetrics? Performance = null);

/// <summary>
/// 表示一次 turn 的缓存与连接池性能指标。
/// </summary>
/// <param name="AgentLaunchPlanCacheHit">是否命中了启动计划缓存。</param>
/// <param name="McpHandshakeAvoided">是否至少复用了一个 MCP 连接。</param>
/// <param name="ReusedMcpConnections">复用的 MCP 连接数量。</param>
/// <param name="TotalMcpConnections">本轮涉及的 MCP 连接总数。</param>
public sealed record PerformanceMetrics(
    bool AgentLaunchPlanCacheHit,
    bool McpHandshakeAvoided,
    int ReusedMcpConnections,
    int TotalMcpConnections);

/// <summary>
/// 组合了运行时所需核心子系统的内核接口。
/// </summary>
public interface IClawKernel
{
    /// <summary>
    /// 当前生效的库配置。
    /// </summary>
    ClawOptions Options { get; }

    /// <summary>
    /// agent 注册表。
    /// </summary>
    IAgentRegistry Agents { get; }

    /// <summary>
    /// skill 注册表。
    /// </summary>
    ISkillRegistry Skills { get; }

    /// <summary>
    /// 工具注册表。
    /// </summary>
    IToolRegistry Tools { get; }

    /// <summary>
    /// 记忆索引。
    /// </summary>
    IMemoryIndex Memory { get; }

    /// <summary>
    /// MCP 客户端管理器。
    /// </summary>
    IMcpClientManager Mcp { get; }

    /// <summary>
    /// session 生命周期管理器。
    /// </summary>
    ISessionManager Sessions { get; }

    /// <summary>
    /// ThreadSpace 生命周期管理器。
    /// </summary>
    IThreadSpaceManager ThreadSpaces { get; }

    /// <summary>
    /// prompt 历史存储。
    /// </summary>
    IPromptHistoryStore History { get; }

    /// <summary>
    /// session 事件存储。
    /// </summary>
    ISessionEventStore Events { get; }

    /// <summary>
    /// provider 解析器。
    /// </summary>
    IModelProviderResolver Providers { get; }

    /// <summary>
    /// 项目脚手架生成器。
    /// </summary>
    IProjectScaffolder Projects { get; }
}

/// <summary>
/// 表示一次 turn 运行过程中的流式事件。
/// </summary>
/// <param name="Delta">增量文本内容；仅在部分消息时存在。</param>
/// <param name="FinalResult">最终结果；仅在流结束时存在。</param>
/// <param name="EventType">自定义事件类型。</param>
/// <param name="EventPayload">自定义事件负载。</param>
public sealed record RunTurnStreamEvent(
    string? Delta = null, 
    RunTurnResult? FinalResult = null, 
    string? EventType = null, 
    System.Text.Json.JsonElement? EventPayload = null);

/// <summary>
/// ClawSharp 对外暴露的主运行时接口。
/// </summary>
/// <remarks>
/// 典型调用顺序为：先 <see cref="InitializeAsync"/>，再启动 session、追加用户消息并执行 turn；
/// 随后可通过 <see cref="GetHistoryAsync"/> 查询历史，或通过 <see cref="CancelSessionAsync"/> 中断 session。
/// </remarks>
public interface IClawRuntime
{
    /// <summary>
    /// 初始化运行时依赖，例如重新加载 agents 与 skills。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在启动 worker 前准备完整的 agent 执行计划。
    /// </summary>
    /// <param name="request">agent 启动请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可直接用于启动 worker 的执行计划。</returns>
    Task<AgentLaunchPlan> PrepareAgentAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据启动计划创建或复用 worker 进程会话。
    /// </summary>
    /// <param name="plan">已解析的启动计划。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可交互的 worker 会话。</returns>
    Task<IAgentWorkerSession> LaunchAgentProcessAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// 为指定 agent 启动一个新的 session。
    /// </summary>
    /// <param name="agentId">agent 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新创建的 runtime session。</returns>
    Task<RuntimeSession> StartSessionAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定 ThreadSpace 中启动一个新的 session。
    /// </summary>
    /// <param name="request">session 启动请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新创建的 runtime session。</returns>
    Task<RuntimeSession> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向 session 追加一条用户消息。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="content">用户消息内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已追加的 prompt 消息。</returns>
    Task<PromptMessage> AppendUserMessageAsync(SessionId sessionId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行 session 的下一次 turn。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="delegation">委派上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次 turn 的最终结果。</returns>
    /// <exception cref="InvalidOperationException">当缺少用户消息或 worker 返回错误时抛出。</exception>
    Task<RunTurnResult> RunTurnAsync(SessionId sessionId, DelegationContext? delegation = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以流式方式执行 session 的下一次 turn。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="delegation">委派上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步流事件。</returns>
    IAsyncEnumerable<RunTurnStreamEvent> RunTurnStreamingAsync(SessionId sessionId, DelegationContext? delegation = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取一个 session 的完整历史视图，包括消息与事件。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按统一顺序号排序的历史条目。</returns>
    Task<IReadOnlyList<PromptHistoryEntry>> GetHistoryAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消一个 session 及其关联 worker。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CancelSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定 session 的所有历史消息与事件。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteSessionDataAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动重新加载 agent、skill 与运行时缓存。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的内核实现，只做依赖聚合而不额外增加业务逻辑。
/// </summary>
public sealed class ClawKernel(
    ClawOptions options,
    IAgentRegistry agents,
    ISkillRegistry skills,
    IToolRegistry tools,
    IMemoryIndex memory,
    IMcpClientManager mcp,
    ISessionManager sessions,
    IThreadSpaceManager threadSpaces,
    IPromptHistoryStore history,
    ISessionEventStore events,
    IModelProviderResolver providers,
    IProjectScaffolder projects) : IClawKernel
{
    /// <inheritdoc />
    public ClawOptions Options => options;

    /// <inheritdoc />
    public IAgentRegistry Agents => agents;

    /// <inheritdoc />
    public ISkillRegistry Skills => skills;

    /// <inheritdoc />
    public IToolRegistry Tools => tools;

    /// <inheritdoc />
    public IMemoryIndex Memory => memory;

    /// <inheritdoc />
    public IMcpClientManager Mcp => mcp;

    /// <inheritdoc />
    public ISessionManager Sessions => sessions;

    /// <inheritdoc />
    public IThreadSpaceManager ThreadSpaces => threadSpaces;

    /// <inheritdoc />
    public IPromptHistoryStore History => history;

    /// <inheritdoc />
    public ISessionEventStore Events => events;

    /// <inheritdoc />
    public IModelProviderResolver Providers => providers;

    /// <inheritdoc />
    public IProjectScaffolder Projects => projects;
}

/// <summary>
/// 默认的 ClawSharp 运行时实现。
/// </summary>
public sealed class ClawRuntime(
    IClawKernel kernel,
    McpService mcpService,
    IMcpServerCatalog serverCatalog,
    ISessionStore sessionStore,
    IAgentWorkerLauncher workerLauncher,
    ISessionSerializer serializer,
    IPermissionResolver permissionResolver,
    IPermissionScopeManager permissionScopeManager,
    IServiceProvider serviceProvider) : IClawRuntime, IDisposable
{
    private readonly Dictionary<string, IAgentWorkerSession> _workers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AgentLaunchPlanCacheEntry> _launchPlanCache = new(StringComparer.OrdinalIgnoreCase);
    private DefinitionWatcher? _watcher;

    private IPermissionUI? PermissionUI => serviceProvider.GetService(typeof(IPermissionUI)) as IPermissionUI;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await mcpService.StartAllAsync(cancellationToken).ConfigureAwait(false);
        await kernel.Agents.ReloadAsync(cancellationToken).ConfigureAwait(false);
        await kernel.Skills.ReloadAsync(cancellationToken).ConfigureAwait(false);

        _watcher = new DefinitionWatcher(kernel.Agents, kernel.Skills);
        _watcher.DefinitionChanged += HandleDefinitionChanged;
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _watcher.Watch(Path.Combine(userHome, ".agent"), isAgent: true);
        _watcher.Watch(Path.Combine(userHome, ".skills"), isAgent: false);

        await kernel.ThreadSpaces.EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);

        // Check for environment dependencies if they are used by any registered tools
        await CheckEnvironmentDependenciesAsync().ConfigureAwait(false);
    }

    private async Task CheckEnvironmentDependenciesAsync()
    {
        var tools = kernel.Tools.GetAll();

        // 1. Playwright Check (Required for web_browser)
        if (tools.Any(t => t.Name == "web_browser"))
        {
            try
            {
                var playwrightCaches = ResolvePlaywrightCachePaths();
                if (!playwrightCaches.Any(path =>
                        Directory.Exists(path) &&
                        Directory.EnumerateDirectories(path, "chromium-*").Any()))
                {
                    throw new EnvironmentDependencyException(
                        "web_browser",
                        "Playwright Chromium browser is missing. This is required for the 'web_browser' tool.",
                        "dotnet run --project ClawSharp.Lib -- playwright install chromium");
                }
            }
            catch (EnvironmentDependencyException) { throw; }
            catch (Exception ex) { Debug.WriteLine($"Error checking Playwright: {ex.Message}"); }
        }

        // 2. MarkItDown Check (Optional for pdf_read)
        if (tools.Any(t => t.Name == "pdf_read"))
        {
            if (!ToolSecurity.CommandExists("markitdown"))
            {
                // We don't throw here because it's optional, but we can log a hint
                Debug.WriteLine("Hint: 'markitdown' not found. Install it via 'pip install markitdown' for better PDF extraction quality.");
            }
        }
    }

    private static IReadOnlyList<string> ResolvePlaywrightCachePaths()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright"));
        }
        else
        {
            paths.Add(Path.Combine(userHome, ".cache", "ms-playwright"));

            if (OperatingSystem.IsMacOS())
            {
                paths.Add(Path.Combine(userHome, "Library", "Caches", "ms-playwright"));
            }
        }

        return paths;
    }

    /// <summary>
    /// 释放运行时资源。
    /// </summary>
    public void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.DefinitionChanged -= HandleDefinitionChanged;
        }

        _watcher?.Dispose();
    }

    /// <inheritdoc />
    public async Task<AgentLaunchPlan> PrepareAgentAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var agent = kernel.Agents.Get(request.AgentId);
        var permissions = permissionResolver.Resolve(agent, kernel.Options.WorkspacePolicy);
        var record = await sessionStore.GetAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
                     ?? throw new KeyNotFoundException($"Session '{request.SessionId}' was not found.");
        var history = await kernel.History.ListAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var session = new RuntimeSession(record, permissions, null);
        var definitionHash = ComputeDefinitionHash(agent, permissions);
        var cacheKey = BuildCacheKey(request.SessionId, request.AgentId);

        if (_launchPlanCache.TryGetValue(cacheKey, out var cached) &&
            string.Equals(cached.DefinitionHash, definitionHash, StringComparison.Ordinal))
        {
            cached.LastUsedAt = DateTimeOffset.UtcNow;
            return new AgentLaunchPlan(
                session,
                cached.Agent,
                cached.Skills,
                cached.Tools,
                cached.McpServers,
                cached.ProviderTarget,
                history,
                CacheHit: true,
                DefinitionHash: cached.DefinitionHash);
        }

        var provider = kernel.Providers.Resolve(agent);
        var skills = agent.Skills.Select(kernel.Skills.Get).ToArray();
        var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (agent.HasExplicitTools)
        {
            foreach (var t in agent.Tools)
            {
                toolNames.Add(t);
            }
        }

        foreach (var t in kernel.Options.WorkspacePolicy.MandatoryTools)
        {
            toolNames.Add(t);
        }

        var tools = kernel.Tools.GetAuthorizedTools(permissions)
            .Where(x => toolNames.Count == 0 || toolNames.Contains(x.Name))
            .ToArray();

        var mcpServers = agent.McpServers
            .Select(serverCatalog.Get)
            .Where(s => s.Capabilities == ToolCapability.None || permissions.Capabilities.HasFlag(s.Capabilities))
            .ToArray();

        _launchPlanCache[cacheKey] = new AgentLaunchPlanCacheEntry
        {
            AgentId = request.AgentId,
            DefinitionHash = definitionHash,
            Agent = agent,
            Skills = skills,
            Tools = tools,
            McpServers = mcpServers,
            ProviderTarget = provider.Target,
            LastUsedAt = DateTimeOffset.UtcNow
        };

        return new AgentLaunchPlan(
            session,
            agent,
            skills,
            tools,
            mcpServers,
            provider.Target,
            history,
            CacheHit: false,
            DefinitionHash: definitionHash);
    }

    /// <inheritdoc />
    public async Task<IAgentWorkerSession> LaunchAgentProcessAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default)
    {
        if (_workers.TryGetValue(plan.Session.Record.SessionId.Value, out var existing))
        {
            return existing;
        }

        var worker = await workerLauncher.LaunchAsync(plan, cancellationToken).ConfigureAwait(false);
        _workers[plan.Session.Record.SessionId.Value] = worker;
        return worker;
    }

    /// <inheritdoc />
    public async Task<RuntimeSession> StartSessionAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var global = await kernel.ThreadSpaces.GetGlobalAsync(cancellationToken).ConfigureAwait(false);
        return await kernel.Sessions.StartAsync(agentId, global.ThreadSpaceId, global.BoundFolderPath ?? kernel.Options.Runtime.WorkspaceRoot, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RuntimeSession> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        var threadSpace = await kernel.ThreadSpaces.GetAsync(request.ThreadSpaceId, cancellationToken).ConfigureAwait(false);
        return await kernel.Sessions.StartAsync(request.AgentId, threadSpace.ThreadSpaceId, threadSpace.BoundFolderPath ?? kernel.Options.Runtime.WorkspaceRoot, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PromptMessage> AppendUserMessageAsync(SessionId sessionId, string content, CancellationToken cancellationToken = default)
    {
        var session = await kernel.Sessions.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var turnId = TurnId.New();
        var message = await kernel.History.AppendAsync(sessionId, turnId, PromptMessageRole.User, content, cancellationToken: cancellationToken).ConfigureAwait(false);
        await kernel.Events.AppendAsync(sessionId, turnId, "MessageAppended", serializer.SerializeToElement(new { role = "user", content }), cancellationToken).ConfigureAwait(false);
        return message;
    }

    /// <inheritdoc />
    public async Task<RunTurnResult> RunTurnAsync(SessionId sessionId, DelegationContext? delegation = null, CancellationToken cancellationToken = default)
    {
        RunTurnResult? result = null;
        await foreach (var @event in RunTurnStreamingAsync(sessionId, delegation, cancellationToken).ConfigureAwait(false))
        {
            if (@event.FinalResult != null)
            {
                result = @event.FinalResult;
            }
        }

        return result ?? throw new InvalidOperationException("Turn failed to produce a result.");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RunTurnStreamEvent> RunTurnStreamingAsync(SessionId sessionId, DelegationContext? delegation = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (delegation != null)
        {
            // Enforce max depth (FR-006)
            if (delegation.CallStack.Count >= kernel.Options.Orchestration.MaxDelegationDepth)
            {
                throw new InvalidOperationException($"Max delegation depth reached ({kernel.Options.Orchestration.MaxDelegationDepth}). Check for infinite loops.");
            }

            // Enforce permission scope if set
            if (delegation.Permissions != null)
            {
                permissionScopeManager.SetScope(delegation.Permissions);
            }
        }

        var session = await kernel.Sessions.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (delegation != null)
        {
            // Circular delegation detection (SC-004)
            var callCount = delegation.CallStack.Count(x => x.Equals(session.Record.AgentId, StringComparison.OrdinalIgnoreCase));
            if (callCount >= 2) // Allow one re-entry but break on the second one (matching "within 3 iterations" loosely or strictly)
            {
                throw new InvalidOperationException($"Circular delegation detected: Agent '{session.Record.AgentId}' has already been called {callCount} times in the current chain.");
            }
        }

        var messages = await kernel.History.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var lastUser = messages.LastOrDefault(x => x.Role == PromptMessageRole.User)
                       ?? throw new InvalidOperationException("A user message is required before running a turn.");

        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "WorkerStarted", serializer.SerializeToElement(new { sessionId = sessionId.Value }), cancellationToken).ConfigureAwait(false);
        await sessionStore.UpdateStatusAsync(sessionId, SessionStatus.Running, cancellationToken: cancellationToken).ConfigureAwait(false);

        var plan = await PrepareAgentAsync(new AgentLaunchRequest(session.Record.AgentId, sessionId), cancellationToken).ConfigureAwait(false);
        
        // --- 自动化 RAG 注入开始 ---
        var augmentedContext = string.Empty;
        try
        {
            // 确定检索命名空间：默认使用当前 Session 的命名空间，
            // 这样可以确保检索的是与当前对话或 Agent 相关的私有记忆 (FR-007)
            var scopeResolver = serviceProvider.GetRequiredService<IMemoryScopeResolver>();
            var sessionScope = scopeResolver.Session(session.Record.ThreadSpaceId.Value, sessionId.Value);

            var topK = kernel.Options.Memory.TopK > 0 ? kernel.Options.Memory.TopK : 5;
            var memoryResults = await kernel.Memory.SearchAsync(
                new MemoryQuery(sessionScope, lastUser.Content, TopK: topK),
                cancellationToken).ConfigureAwait(false);

            if (memoryResults.Count > 0)
            {
                augmentedContext = "\n[Relevant Background Knowledge]:\n" +
                    string.Join("\n---\n", memoryResults.Select(r => r.Content));
            }
        }
        catch (Exception ex)
        {
            // RAG 失败不应中断对话
            System.Diagnostics.Debug.WriteLine($"RAG Retrieval failed: {ex.Message}");
        }
        // --- 自动化 RAG 注入结束 ---
        var reusedMcpConnections = 0;
        foreach (var mcpServer in plan.McpServers)
        {
            var hadExistingConnection = kernel.Mcp.GetConnectedSessions()
                .Any(connected => connected.ServerName.Equals(mcpServer.Name, StringComparison.OrdinalIgnoreCase));
            await kernel.Mcp.ConnectAsync(mcpServer.Name, cancellationToken).ConfigureAwait(false);
            if (hadExistingConnection)
            {
                reusedMcpConnections++;
            }
        }

        var worker = await LaunchAgentProcessAsync(plan, cancellationToken).ConfigureAwait(false);
        await worker.InitializeAsync(new WorkerInitializeRequest(sessionId.Value, plan.Agent.Id, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);

        var modelMessages = BuildModelMessages(plan.Agent, messages);
        
        // 如果有检索到的背景知识，将其注入到系统提示中或作为第一条增强消息
        var finalSystemPrompt = plan.Agent.SystemPrompt;
        if (!string.IsNullOrEmpty(augmentedContext))
        {
            finalSystemPrompt += "\n\n" + augmentedContext;
        }

        var toolSchemas = plan.Tools.Select(tool => new ModelToolSchema(tool.Name, tool.Description, tool.InputSchema?.GetRawText())).ToArray();
        var request = new WorkerTurnRequest(
            sessionId.Value,
            lastUser.TurnId.Value,
            Guid.NewGuid().ToString("N"),
            plan.ProviderTarget,
            modelMessages,
            toolSchemas,
            finalSystemPrompt);

        var assistant = new List<string>();
        var totalToolCalls = 0;
        ModelUsage? latestUsage = null;
        var turnTimer = Stopwatch.StartNew();
        bool turnSuccess = false;

        try
        {
            await foreach (var workerEvent in worker.RunTurnAsync(
                               request,
                               toolRequest => HandleToolRequestAsync(plan, lastUser, toolRequest, delegation, cancellationToken),
                               cancellationToken).ConfigureAwait(false))
            {
                switch (workerEvent.EventType)
                {
                    case "worker.message.delta":
                        var delta = workerEvent.Payload.GetProperty("delta").GetString() ?? string.Empty;
                        assistant.Add(delta);
                        yield return new RunTurnStreamEvent(Delta: delta);

                        if (kernel.Options.History.RecordMessageDeltas)
                        {
                            await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "MessageDelta", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        }
                        break;
                    case "worker.tool.requested":
                        totalToolCalls++;
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "ToolCallRequested", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        var toolName = workerEvent.Payload.GetProperty("name").GetString();
                        var isAgent = kernel.Agents.GetAll().Any(a => a.Id.Equals(toolName, StringComparison.OrdinalIgnoreCase) || a.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                        var label = isAgent ? "Delegating to agent" : "Calling tool";
                        yield return new RunTurnStreamEvent(Delta: $"\n[{label}: {toolName}]... ");
                        break;
                    case "worker.tool.completed":
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "ToolCallCompleted", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        yield return new RunTurnStreamEvent(Delta: "[Done]\n");
                        break;
                    case "worker.usage.updated":
                        if (workerEvent.Payload.TryGetProperty("usage", out var usagePayload))
                        {
                            latestUsage = ParseUsage(usagePayload);
                        }
                        break;
                    case "worker.error":
                        var errorMsg = workerEvent.Payload.TryGetProperty("message", out var m) ? m.GetString() : workerEvent.Payload.GetRawText();
                        yield return new RunTurnStreamEvent(Delta: $"\n[ERROR: {errorMsg}]\n");
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "TurnFailed", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        throw new InvalidOperationException(errorMsg);
                    case "worker.state.changed":
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "WorkerStateChanged", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }


            var finalAssistant = string.Concat(assistant);
            if (!string.IsNullOrWhiteSpace(finalAssistant))
            {
                await kernel.History.AppendBlocksAsync(
                        sessionId,
                        lastUser.TurnId,
                        PromptMessageRole.Assistant,
                        [new ModelTextBlock(finalAssistant)],
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await kernel.Events.AppendAsync(
                    sessionId,
                    lastUser.TurnId,
                    "TurnCompleted",
                    serializer.SerializeToElement(new
                    {
                        content = finalAssistant,
                        toolCalls = totalToolCalls,
                        usage = latestUsage,
                        latencyMs = turnTimer.Elapsed.TotalMilliseconds,
                        cacheHit = plan.CacheHit,
                        mcpHandshakeAvoided = reusedMcpConnections > 0,
                        performance = new
                        {
                            agentLaunchPlanCacheHit = plan.CacheHit,
                            reusedMcpConnections,
                            totalMcpConnections = plan.McpServers.Count,
                            mcpHandshakeAvoided = reusedMcpConnections > 0
                        },
                        blocks = string.IsNullOrWhiteSpace(finalAssistant)
                            ? Array.Empty<object>()
                            : new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = finalAssistant
                                }
                            }
                    }),
                    cancellationToken)
                .ConfigureAwait(false);
            await sessionStore.UpdateStatusAsync(sessionId, SessionStatus.Completed, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

            turnSuccess = true;
            var performance = new PerformanceMetrics(
                plan.CacheHit,
                reusedMcpConnections > 0,
                reusedMcpConnections,
                plan.McpServers.Count);
            var finalResult = new RunTurnResult(
                sessionId,
                lastUser.TurnId,
                finalAssistant,
                SessionStatus.Completed,
                totalToolCalls,
                performance);
            yield return new RunTurnStreamEvent(FinalResult: finalResult);
        }
        finally
        {
            foreach (var mcpServer in plan.McpServers)
            {
                await kernel.Mcp.ReleaseAsync(mcpServer.Name, cancellationToken).ConfigureAwait(false);
            }

            if (!turnSuccess)
            {
                var status = cancellationToken.IsCancellationRequested ? SessionStatus.Cancelled : SessionStatus.Failed;
                await sessionStore.UpdateStatusAsync(sessionId, status, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static ModelUsage? ParseUsage(JsonElement usagePayload)
    {
        var inputTokens = ReadInt32(usagePayload, "InputTokens", "input_tokens", "PromptTokens", "prompt_tokens");
        var outputTokens = ReadInt32(usagePayload, "OutputTokens", "output_tokens", "CompletionTokens", "completion_tokens");
        var totalTokens = ReadInt32(usagePayload, "TotalTokens", "total_tokens");

        if (inputTokens == 0 && outputTokens == 0 && totalTokens == 0)
        {
            return null;
        }

        if (totalTokens == 0)
        {
            totalTokens = inputTokens + outputTokens;
        }

        return new ModelUsage(inputTokens, outputTokens, totalTokens);
    }

    private static int ReadInt32(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromptHistoryEntry>> GetHistoryAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        var messages = await kernel.History.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var events = await kernel.Events.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);

        return messages.Select(message => new PromptHistoryEntry(message.SequenceNo, Message: message))
            .Concat(events.Select(@event => new PromptHistoryEntry(@event.SequenceNo, Event: @event)))
            .OrderBy(entry => entry.SequenceNo)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task CancelSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        if (_workers.TryGetValue(sessionId.Value, out var worker))
        {
            await worker.CancelAsync(cancellationToken).ConfigureAwait(false);
        }

        EvictSessionCache(sessionId);
        await kernel.Sessions.CancelAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteSessionDataAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        EvictSessionCache(sessionId);
        await kernel.History.DeleteBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        await kernel.Events.DeleteBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _launchPlanCache.Clear();
        await kernel.Agents.ReloadAsync(cancellationToken).ConfigureAwait(false);
        await kernel.Skills.ReloadAsync(cancellationToken).ConfigureAwait(false);
        await mcpService.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerToolResult> HandleToolRequestAsync(
        AgentLaunchPlan plan,
        PromptMessage userMessage,
        WorkerToolRequest toolRequest,
        DelegationContext? delegation,
        CancellationToken cancellationToken)
    {
        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(toolRequest.ArgumentsJson) ? "{}" : toolRequest.ArgumentsJson);
            arguments = JsonSerializer.SerializeToElement(document.RootElement);
        }
        catch (JsonException ex)
        {
            return new WorkerToolResult(toolRequest.ToolCallId, ToolInvocationResult.Failure(toolRequest.ToolName, ex.Message));
        }

        // 获取工具定义以检查所需能力
        var toolDef = kernel.Tools.GetAll().FirstOrDefault(t => t.Name.Equals(toolRequest.ToolName, StringComparison.OrdinalIgnoreCase));
        var currentPermissions = plan.Session.EffectivePermissions ?? ToolPermissionSet.Empty;

        if (toolDef != null && (toolDef.Capabilities & currentPermissions.Capabilities) != toolDef.Capabilities)
        {
            var missing = toolDef.Capabilities & ~currentPermissions.Capabilities;
            
            await kernel.Events.AppendAsync(plan.Session.Record.SessionId, userMessage.TurnId, "JitElevationRequested", serializer.SerializeToElement(new { tool = toolRequest.ToolName, capability = missing.ToString() }), cancellationToken).ConfigureAwait(false);

            if (PermissionUI != null && await PermissionUI.RequestCapabilityAsync(plan.Agent.Id, missing, toolRequest.ToolName, cancellationToken).ConfigureAwait(false))
            {
                // 用户批准，提升权限
                currentPermissions = currentPermissions with { Capabilities = currentPermissions.Capabilities | missing };
                await kernel.Events.AppendAsync(plan.Session.Record.SessionId, userMessage.TurnId, "JitElevationGranted", serializer.SerializeToElement(new { tool = toolRequest.ToolName, capability = missing.ToString() }), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // 用户拒绝
                await kernel.Events.AppendAsync(plan.Session.Record.SessionId, userMessage.TurnId, "JitElevationDenied", serializer.SerializeToElement(new { tool = toolRequest.ToolName, capability = missing.ToString() }), cancellationToken).ConfigureAwait(false);
                return new WorkerToolResult(toolRequest.ToolCallId, ToolInvocationResult.Failure(toolRequest.ToolName, $"Permission denied: Tool requires missing capability '{missing}'."));
            }
        }

        // 运行时在这里把 worker 请求转换为统一工具上下文，并处理审批状态与历史落盘。
        var context = new ToolExecutionContext(
            plan.Session.Record.WorkspaceRoot,
            plan.Agent.Id,
            plan.Session.Record.SessionId.Value,
            userMessage.TurnId.Value,
            userMessage.MessageId.Value,
            currentPermissions,
            Guid.NewGuid().ToString("N"),
            cancellationToken,
            delegation);
        var result = await kernel.Tools.ExecuteAsync(toolRequest.ToolName, context, arguments).ConfigureAwait(false);
        if (result.Status == ToolInvocationStatus.ApprovalRequired)
        {
            await sessionStore.UpdateStatusAsync(plan.Session.Record.SessionId, SessionStatus.WaitingForApproval, cancellationToken: cancellationToken).ConfigureAwait(false);
            await kernel.Events.AppendAsync(plan.Session.Record.SessionId, userMessage.TurnId, "ApprovalRequested", serializer.SerializeToElement(new { tool = toolRequest.ToolName }), cancellationToken).ConfigureAwait(false);
        }

        if (kernel.Options.History.RecordToolPayloads)
        {
            await kernel.History.AppendBlocksAsync(
                    plan.Session.Record.SessionId,
                    userMessage.TurnId,
                    PromptMessageRole.Tool,
                    [new ModelToolResultBlock(toolRequest.ToolCallId, result.Payload.GetRawText(), toolRequest.ToolName)],
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new WorkerToolResult(toolRequest.ToolCallId, result);
    }

    private static IReadOnlyList<ModelMessage> BuildModelMessages(AgentDefinition agent, IReadOnlyList<PromptMessage> messages)
    {
        var modelMessages = new List<ModelMessage>();
        if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
        {
            modelMessages.Add(new ModelMessage(ModelMessageRole.System, agent.SystemPrompt));
        }

        modelMessages.AddRange(messages.Select(ToModelMessage));

        return modelMessages;
    }

    private static ModelMessage ToModelMessage(PromptMessage message)
    {
        var role = message.Role switch
        {
            PromptMessageRole.System => ModelMessageRole.System,
            PromptMessageRole.User => ModelMessageRole.User,
            PromptMessageRole.Assistant => ModelMessageRole.Assistant,
            PromptMessageRole.Tool => ModelMessageRole.Tool,
            _ => ModelMessageRole.User
        };

        if (role == ModelMessageRole.Tool && !string.IsNullOrWhiteSpace(message.ToolCallId))
        {
            return ModelMessage.ToolResult(message.ToolCallId, message.Content, message.Name);
        }

        if (role == ModelMessageRole.Assistant &&
            !string.IsNullOrWhiteSpace(message.ToolCallId) &&
            !string.IsNullOrWhiteSpace(message.Name))
        {
            return ModelMessage.AssistantToolUse(message.ToolCallId, message.Name, message.Content);
        }

        return new ModelMessage(role, message.Content);
    }

    private void HandleDefinitionChanged(DefinitionChangedEvent change)
    {
        _launchPlanCache.Clear();
    }

    private static string BuildCacheKey(SessionId sessionId, string agentId) => $"{sessionId.Value}:{agentId}";

    private void EvictSessionCache(SessionId sessionId)
    {
        var prefix = $"{sessionId.Value}:";
        foreach (var cacheKey in _launchPlanCache.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _launchPlanCache.TryRemove(cacheKey, out _);
        }
    }

    private static string ComputeDefinitionHash(AgentDefinition agent, ToolPermissionSet permissions)
    {
        var payload = JsonSerializer.Serialize(new
        {
            agent.Id,
            agent.Name,
            agent.Description,
            agent.Provider,
            agent.Model,
            agent.SystemPrompt,
            agent.Tools,
            agent.Skills,
            agent.MemoryScope,
            agent.McpServers,
            agent.Version,
            agent.Body,
            permissions.Capabilities,
            permissions.ApprovalRequired
        });

        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
