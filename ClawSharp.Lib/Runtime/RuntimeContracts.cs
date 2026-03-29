using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;
using System.Text.Json;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 运行时视角下的 session 快照。
/// </summary>
/// <param name="Record">底层 session 记录。</param>
/// <param name="EffectivePermissions">当前已解析出的有效权限；未解析时为 <see langword="null"/>。</param>
/// <param name="WorkerHandle">关联的 worker 句柄；未启动 worker 时为 <see langword="null"/>。</param>
public sealed record RuntimeSession(SessionRecord Record, ToolPermissionSet? EffectivePermissions, AgentProcessHandle? WorkerHandle);

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
public sealed record AgentLaunchPlan(
    RuntimeSession Session,
    AgentDefinition Agent,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<ToolDefinition> Tools,
    IReadOnlyList<McpServerDefinition> McpServers,
    ResolvedModelTarget ProviderTarget,
    IReadOnlyList<PromptMessage> History);

/// <summary>
/// 表示一次 turn 运行完成后的摘要结果。
/// </summary>
/// <param name="SessionId">所属 session。</param>
/// <param name="TurnId">本次执行的 turn 标识。</param>
/// <param name="AssistantMessage">assistant 最终回复文本。</param>
/// <param name="Status">turn 完成后的 session 状态。</param>
/// <param name="ToolCallCount">本次 turn 中发生的工具调用次数。</param>
public sealed record RunTurnResult(SessionId SessionId, TurnId TurnId, string AssistantMessage, SessionStatus Status, int ToolCallCount);

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
}

/// <summary>
/// ClawSharp 对外暴露的主运行时接口。
/// </summary>
/// <remarks>
/// 典型调用顺序为：<see cref="InitializeAsync"/> -> <see cref="StartSessionAsync"/> ->
/// <see cref="AppendUserMessageAsync"/> -> <see cref="RunTurnAsync"/>，随后可通过
/// <see cref="GetHistoryAsync"/> 查询历史，或通过 <see cref="CancelSessionAsync"/> 中断 session。
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
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次 turn 的最终结果。</returns>
    /// <exception cref="InvalidOperationException">当缺少用户消息或 worker 返回错误时抛出。</exception>
    Task<RunTurnResult> RunTurnAsync(SessionId sessionId, CancellationToken cancellationToken = default);

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
    IPromptHistoryStore history,
    ISessionEventStore events,
    IModelProviderResolver providers) : IClawKernel
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
    public IPromptHistoryStore History => history;

    /// <inheritdoc />
    public ISessionEventStore Events => events;

    /// <inheritdoc />
    public IModelProviderResolver Providers => providers;
}

/// <summary>
/// 默认的 ClawSharp 运行时实现。
/// </summary>
public sealed class ClawRuntime(
    IClawKernel kernel,
    IMcpServerCatalog serverCatalog,
    ISessionStore sessionStore,
    IAgentWorkerLauncher workerLauncher,
    ISessionSerializer serializer) : IClawRuntime
{
    private readonly Dictionary<string, IAgentWorkerSession> _workers = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await kernel.Agents.ReloadAsync(cancellationToken).ConfigureAwait(false);
        await kernel.Skills.ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AgentLaunchPlan> PrepareAgentAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var agent = kernel.Agents.Get(request.AgentId);
        var permissions = agent.Permissions.Merge(kernel.Options.WorkspacePolicy.Permissions);
        var provider = kernel.Providers.Resolve(agent);
        var record = await sessionStore.GetAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
                     ?? throw new KeyNotFoundException($"Session '{request.SessionId}' was not found.");
        var history = await kernel.History.ListAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var session = new RuntimeSession(record, permissions, null);

        var skills = agent.Skills.Select(kernel.Skills.Get).ToArray();
        var tools = kernel.Tools.GetAuthorizedTools(permissions)
            .Where(x => agent.Tools.Count == 0 || agent.Tools.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var mcpServers = agent.McpServers.Select(serverCatalog.Get).ToArray();

        return new AgentLaunchPlan(session, agent, skills, tools, mcpServers, provider.Target, history);
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
    public Task<RuntimeSession> StartSessionAsync(string agentId, CancellationToken cancellationToken = default)
    {
        return kernel.Sessions.StartAsync(agentId, kernel.Options.Runtime.WorkspaceRoot, cancellationToken);
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
    public async Task<RunTurnResult> RunTurnAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        var session = await kernel.Sessions.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var messages = await kernel.History.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var lastUser = messages.LastOrDefault(x => x.Role == PromptMessageRole.User)
                       ?? throw new InvalidOperationException("A user message is required before running a turn.");

        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "WorkerStarted", serializer.SerializeToElement(new { sessionId = sessionId.Value }), cancellationToken).ConfigureAwait(false);
        await sessionStore.UpdateStatusAsync(sessionId, SessionStatus.Running, cancellationToken: cancellationToken).ConfigureAwait(false);

        var plan = await PrepareAgentAsync(new AgentLaunchRequest(session.Record.AgentId, sessionId), cancellationToken).ConfigureAwait(false);
        foreach (var mcpServer in plan.McpServers)
        {
            await kernel.Mcp.ConnectAsync(mcpServer.Name, cancellationToken).ConfigureAwait(false);
        }

        var worker = await LaunchAgentProcessAsync(plan, cancellationToken).ConfigureAwait(false);
        await worker.InitializeAsync(new WorkerInitializeRequest(sessionId.Value, plan.Agent.Id, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);

        var modelMessages = BuildModelMessages(plan.Agent, messages);
        var toolSchemas = plan.Tools.Select(tool => new ModelToolSchema(tool.Name, tool.Description, tool.InputSchema?.GetRawText())).ToArray();
        var request = new WorkerTurnRequest(
            sessionId.Value,
            lastUser.TurnId.Value,
            Guid.NewGuid().ToString("N"),
            plan.ProviderTarget,
            modelMessages,
            toolSchemas,
            plan.Agent.SystemPrompt);

        var assistant = new List<string>();
        var toolCalls = 0;

        try
        {
            // turn 期间的状态变化、增量文本和工具事件都会写入历史事件流，便于调试和后续 UI 回放。
            await foreach (var workerEvent in worker.RunTurnAsync(
                               request,
                               toolRequest => HandleToolRequestAsync(plan, lastUser, toolRequest, cancellationToken),
                               cancellationToken).ConfigureAwait(false))
            {
                switch (workerEvent.EventType)
                {
                    case "worker.message.delta":
                        var delta = workerEvent.Payload.GetProperty("delta").GetString() ?? string.Empty;
                        assistant.Add(delta);
                        if (kernel.Options.History.RecordMessageDeltas)
                        {
                            await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "MessageDelta", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        }
                        break;
                    case "worker.tool.requested":
                        toolCalls++;
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "ToolCallRequested", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        break;
                    case "worker.tool.completed":
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "ToolCallCompleted", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        break;
                    case "worker.state.changed":
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "WorkerStateChanged", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        break;
                    case "worker.error":
                        await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "TurnFailed", workerEvent.Payload, cancellationToken).ConfigureAwait(false);
                        await sessionStore.UpdateStatusAsync(sessionId, SessionStatus.Failed, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                        throw new InvalidOperationException(workerEvent.Payload.GetRawText());
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
                        toolCalls,
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
            return new RunTurnResult(sessionId, lastUser.TurnId, finalAssistant, SessionStatus.Completed, toolCalls);
        }
        catch (OperationCanceledException)
        {
            await sessionStore.UpdateStatusAsync(sessionId, SessionStatus.Cancelled, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await sessionStore.UpdateStatusAsync(sessionId, SessionStatus.Failed, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            throw;
        }
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

        await kernel.Sessions.CancelAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerToolResult> HandleToolRequestAsync(
        AgentLaunchPlan plan,
        PromptMessage userMessage,
        WorkerToolRequest toolRequest,
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

        // 运行时在这里把 worker 请求转换为统一工具上下文，并处理审批状态与历史落盘。
        var context = new ToolExecutionContext(
            plan.Session.Record.WorkspaceRoot,
            plan.Agent.Id,
            plan.Session.Record.SessionId.Value,
            userMessage.TurnId.Value,
            userMessage.MessageId.Value,
            plan.Session.EffectivePermissions ?? ToolPermissionSet.Empty,
            Guid.NewGuid().ToString("N"),
            cancellationToken);
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
}
