using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;
using System.Text.Json;

namespace ClawSharp.Lib.Runtime;

public sealed record RuntimeSession(SessionRecord Record, ToolPermissionSet? EffectivePermissions, AgentProcessHandle? WorkerHandle);

public sealed record AgentLaunchRequest(string AgentId, SessionId SessionId);

public sealed record AgentLaunchPlan(
    RuntimeSession Session,
    AgentDefinition Agent,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<ToolDefinition> Tools,
    IReadOnlyList<McpServerDefinition> McpServers,
    ModelProviderMetadata Provider,
    IReadOnlyList<PromptMessage> History);

public sealed record RunTurnResult(SessionId SessionId, TurnId TurnId, string AssistantMessage, SessionStatus Status, int ToolCallCount);

public interface IClawKernel
{
    ClawOptions Options { get; }

    IAgentRegistry Agents { get; }

    ISkillRegistry Skills { get; }

    IToolRegistry Tools { get; }

    IMemoryIndex Memory { get; }

    IMcpClientManager Mcp { get; }

    ISessionManager Sessions { get; }

    IPromptHistoryStore History { get; }

    ISessionEventStore Events { get; }

    IModelProviderResolver Providers { get; }
}

public interface IClawRuntime
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<AgentLaunchPlan> PrepareAgentAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default);

    Task<IAgentWorkerSession> LaunchAgentProcessAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default);

    Task<RuntimeSession> StartSessionAsync(string agentId, CancellationToken cancellationToken = default);

    Task<PromptMessage> AppendUserMessageAsync(SessionId sessionId, string content, CancellationToken cancellationToken = default);

    Task<RunTurnResult> RunTurnAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptHistoryEntry>> GetHistoryAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task CancelSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

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
    public ClawOptions Options => options;

    public IAgentRegistry Agents => agents;

    public ISkillRegistry Skills => skills;

    public IToolRegistry Tools => tools;

    public IMemoryIndex Memory => memory;

    public IMcpClientManager Mcp => mcp;

    public ISessionManager Sessions => sessions;

    public IPromptHistoryStore History => history;

    public ISessionEventStore Events => events;

    public IModelProviderResolver Providers => providers;
}

public sealed class ClawRuntime(
    IClawKernel kernel,
    IMcpServerCatalog serverCatalog,
    ISessionStore sessionStore,
    IAgentWorkerLauncher workerLauncher,
    ISessionSerializer serializer) : IClawRuntime
{
    private readonly Dictionary<string, IAgentWorkerSession> _workers = new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await kernel.Agents.ReloadAsync(cancellationToken).ConfigureAwait(false);
        await kernel.Skills.ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

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

        return new AgentLaunchPlan(session, agent, skills, tools, mcpServers, provider.Metadata, history);
    }

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

    public Task<RuntimeSession> StartSessionAsync(string agentId, CancellationToken cancellationToken = default)
    {
        return kernel.Sessions.StartAsync(agentId, kernel.Options.Runtime.WorkspaceRoot, cancellationToken);
    }

    public async Task<PromptMessage> AppendUserMessageAsync(SessionId sessionId, string content, CancellationToken cancellationToken = default)
    {
        var session = await kernel.Sessions.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var turnId = TurnId.New();
        var message = await kernel.History.AppendAsync(sessionId, turnId, PromptMessageRole.User, content, cancellationToken: cancellationToken).ConfigureAwait(false);
        await kernel.Events.AppendAsync(sessionId, turnId, "MessageAppended", serializer.SerializeToElement(new { role = "user", content }), cancellationToken).ConfigureAwait(false);
        return message;
    }

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
            modelMessages,
            toolSchemas,
            plan.Agent.Model,
            plan.Provider.Name,
            plan.Agent.SystemPrompt);

        var assistant = new List<string>();
        var toolCalls = 0;

        try
        {
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
                await kernel.History.AppendAsync(sessionId, lastUser.TurnId, PromptMessageRole.Assistant, finalAssistant, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await kernel.Events.AppendAsync(sessionId, lastUser.TurnId, "TurnCompleted", serializer.SerializeToElement(new { content = finalAssistant, toolCalls }), cancellationToken).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<PromptHistoryEntry>> GetHistoryAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        var messages = await kernel.History.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var events = await kernel.Events.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);

        return messages.Select(message => new PromptHistoryEntry(message.SequenceNo, Message: message))
            .Concat(events.Select(@event => new PromptHistoryEntry(@event.SequenceNo, Event: @event)))
            .OrderBy(entry => entry.SequenceNo)
            .ToArray();
    }

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
            await kernel.History.AppendAsync(
                    plan.Session.Record.SessionId,
                    userMessage.TurnId,
                    PromptMessageRole.Tool,
                    result.Payload.GetRawText(),
                    name: toolRequest.ToolName,
                    toolCallId: toolRequest.ToolCallId,
                    cancellationToken: cancellationToken)
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

        modelMessages.AddRange(messages.Select(message => new ModelMessage(
            message.Role switch
            {
                PromptMessageRole.System => ModelMessageRole.System,
                PromptMessageRole.User => ModelMessageRole.User,
                PromptMessageRole.Assistant => ModelMessageRole.Assistant,
                PromptMessageRole.Tool => ModelMessageRole.Tool,
                _ => ModelMessageRole.User
            },
            message.Content,
            message.Name,
            message.ToolCallId)));

        return modelMessages;
    }
}
