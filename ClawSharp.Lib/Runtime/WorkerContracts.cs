using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// worker JSON-RPC 消息封装。
/// </summary>
/// <param name="Id">请求标识。</param>
/// <param name="Method">方法名。</param>
/// <param name="Payload">请求 payload。</param>
public sealed record WorkerRpcEnvelope(string Id, string Method, JsonElement Payload);

/// <summary>
/// worker 请求模型。
/// </summary>
/// <param name="RequestId">请求标识。</param>
/// <param name="Method">方法名。</param>
/// <param name="Payload">请求 payload。</param>
public sealed record WorkerRequest(string RequestId, string Method, JsonElement Payload);

/// <summary>
/// worker 响应模型。
/// </summary>
/// <param name="RequestId">对应的请求标识。</param>
/// <param name="Success">请求是否成功。</param>
/// <param name="Payload">响应 payload。</param>
/// <param name="Error">失败时的错误描述。</param>
public sealed record WorkerResponse(string RequestId, bool Success, JsonElement Payload, string? Error = null);

/// <summary>
/// worker 主动推送的事件。
/// </summary>
/// <param name="EventType">事件类型。</param>
/// <param name="Payload">事件 payload。</param>
public sealed record WorkerEvent(string EventType, JsonElement Payload);

/// <summary>
/// worker 初始化请求。
/// </summary>
/// <param name="SessionId">session 标识。</param>
/// <param name="AgentId">agent 标识。</param>
/// <param name="TraceId">追踪标识。</param>
public sealed record WorkerInitializeRequest(string SessionId, string AgentId, string TraceId);

/// <summary>
/// worker 执行一个 turn 所需的完整请求。
/// </summary>
/// <param name="SessionId">session 标识。</param>
/// <param name="TurnId">turn 标识。</param>
/// <param name="TraceId">追踪标识。</param>
/// <param name="Target">已解析的 provider 目标。</param>
/// <param name="Messages">输入消息列表。</param>
/// <param name="Tools">可用工具 schema 列表。</param>
/// <param name="SystemPrompt">可选系统提示词。</param>
public sealed record WorkerTurnRequest(
    string SessionId,
    string TurnId,
    string TraceId,
    ResolvedModelTarget Target,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ModelToolSchema> Tools,
    string? SystemPrompt);

/// <summary>
/// worker 发起的工具调用请求。
/// </summary>
/// <param name="ToolCallId">tool call 标识。</param>
/// <param name="ToolName">工具名。</param>
/// <param name="ArgumentsJson">原始参数 JSON。</param>
public sealed record WorkerToolRequest(string ToolCallId, string ToolName, string ArgumentsJson);

/// <summary>
/// runtime 返回给 worker 的工具调用结果。
/// </summary>
/// <param name="ToolCallId">tool call 标识。</param>
/// <param name="Result">工具执行结果。</param>
public sealed record WorkerToolResult(string ToolCallId, ToolInvocationResult Result);

/// <summary>
/// 描述一个 worker 进程或会话的连接句柄。
/// </summary>
/// <param name="ProcessId">进程标识；进程内 loopback worker 时可为 <see langword="null"/>。</param>
/// <param name="CommandLine">启动命令行。</param>
/// <param name="IsConnected">是否仍然连接。</param>
/// <param name="WorkerSessionId">worker 侧关联的 session 标识。</param>
/// <param name="StartedAt">启动时间。</param>
/// <param name="ReadyAt">准备就绪时间。</param>
public sealed record AgentProcessHandle(int? ProcessId, string? CommandLine, bool IsConnected, string? WorkerSessionId, DateTimeOffset StartedAt, DateTimeOffset? ReadyAt = null);

/// <summary>
/// agent worker 会话抽象。
/// </summary>
public interface IAgentWorkerSession : IAsyncDisposable
{
    /// <summary>
    /// 当前 worker 的进程或连接句柄。
    /// </summary>
    AgentProcessHandle Handle { get; }

    /// <summary>
    /// 初始化 worker 会话。
    /// </summary>
    /// <param name="request">初始化请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task InitializeAsync(WorkerInitializeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求 worker 执行一次 turn，并流式返回事件。
    /// </summary>
    /// <param name="request">turn 请求。</param>
    /// <param name="onToolRequest">处理工具调用的回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>worker 事件流。</returns>
    IAsyncEnumerable<WorkerEvent> RunTurnAsync(WorkerTurnRequest request, Func<WorkerToolRequest, Task<WorkerToolResult>> onToolRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消当前执行中的 turn。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CancelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求 worker 结束自身。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 负责将外部进程连接为 <see cref="IAgentWorkerSession"/> 的客户端抽象。
/// </summary>
public interface IAgentWorkerClient
{
    /// <summary>
    /// 将已启动的进程包装为 worker 会话。
    /// </summary>
    /// <param name="process">已启动进程。</param>
    /// <param name="plan">当前 agent 启动计划。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可交互的 worker 会话。</returns>
    Task<IAgentWorkerSession> ConnectAsync(Process process, AgentLaunchPlan plan, CancellationToken cancellationToken = default);
}

/// <summary>
/// 负责启动 worker 会话的抽象。
/// </summary>
public interface IAgentWorkerLauncher
{
    /// <summary>
    /// 根据启动计划创建一个 worker 会话。
    /// </summary>
    /// <param name="plan">agent 启动计划。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可交互的 worker 会话。</returns>
    Task<IAgentWorkerSession> LaunchAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的 worker 启动器。
/// </summary>
/// <param name="options">库配置。</param>
/// <param name="workerClient">外部 worker 客户端。</param>
/// <param name="providerRegistry">provider 注册表，用于 loopback 模式解析 provider。</param>
public sealed class DefaultAgentWorkerLauncher(
    ClawOptions options,
    IAgentWorkerClient workerClient,
    IModelProviderRegistry providerRegistry) : IAgentWorkerLauncher
{
    /// <inheritdoc />
    public async Task<IAgentWorkerSession> LaunchAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default)
    {
        var command = options.Worker.Command ?? options.Runtime.AgentWorkerCommand;
        if (string.IsNullOrWhiteSpace(command))
        {
            var provider = providerRegistry.Get(plan.ProviderTarget.ProviderType);
            return new LoopbackAgentWorkerSession(plan, provider);
        }

        var arguments = options.Worker.Arguments;
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(command, arguments)
            {
                WorkingDirectory = options.Runtime.WorkspaceRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        return await workerClient.ConnectAsync(process, plan, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 进程内 loopback worker，会直接调用 provider 并把工具请求回调给 runtime。
/// </summary>
/// <param name="plan">当前 agent 启动计划。</param>
/// <param name="provider">要使用的模型 provider。</param>
public sealed class LoopbackAgentWorkerSession(AgentLaunchPlan plan, IModelProvider provider) : IAgentWorkerSession
{
    private CancellationTokenSource? _runCancellation;

    /// <inheritdoc />
    public AgentProcessHandle Handle { get; } = new(null, null, true, plan.Session.Record.SessionId.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    /// <inheritdoc />
    public Task InitializeAsync(WorkerInitializeRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkerEvent> RunTurnAsync(
        WorkerTurnRequest request,
        Func<WorkerToolRequest, Task<WorkerToolResult>> onToolRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var messages = request.Messages.ToList();
        var assistantBuffer = new StringBuilder();

        while (!_runCancellation.IsCancellationRequested)
        {
            var modelRequest = new ModelRequest(
                request.Target,
                request.SessionId,
                request.TraceId,
                messages,
                request.Tools,
                request.SystemPrompt);

            var hasToolCall = false;
            await foreach (var chunk in provider.StreamAsync(modelRequest, _runCancellation.Token).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.TextDelta))
                {
                    assistantBuffer.Append(chunk.TextDelta);
                    yield return new WorkerEvent("worker.message.delta", JsonSerializer.SerializeToElement(new
                    {
                        delta = chunk.TextDelta,
                        blocks = new[]
                        {
                            new
                            {
                                type = "text",
                                text = chunk.TextDelta
                            }
                        }
                    }));
                }

                if (chunk.ToolCall is not null)
                {
                    hasToolCall = true;
                    if (assistantBuffer.Length > 0)
                    {
                        messages.Add(new ModelMessage(ModelMessageRole.Assistant, assistantBuffer.ToString()));
                        assistantBuffer.Clear();
                    }

                    // 为需要原生 tool_use 上下文的 provider 保留 assistant -> tool_use 这一跳。
                    messages.Add(ModelMessage.AssistantToolUse(chunk.ToolCall.Id, chunk.ToolCall.Name, chunk.ToolCall.ArgumentsJson));
                    yield return new WorkerEvent("worker.tool.requested", JsonSerializer.SerializeToElement(new
                    {
                        id = chunk.ToolCall.Id,
                        name = chunk.ToolCall.Name,
                        argumentsJson = chunk.ToolCall.ArgumentsJson,
                        blocks = new[]
                        {
                            new
                            {
                                type = "tool_use",
                                id = chunk.ToolCall.Id,
                                name = chunk.ToolCall.Name,
                                arguments = chunk.ToolCall.ArgumentsJson
                            }
                        }
                    }));
                    var toolResult = await onToolRequest(new WorkerToolRequest(chunk.ToolCall.Id, chunk.ToolCall.Name, chunk.ToolCall.ArgumentsJson)).ConfigureAwait(false);
                    yield return new WorkerEvent("worker.tool.completed", JsonSerializer.SerializeToElement(new
                    {
                        toolCallId = toolResult.ToolCallId,
                        toolName = chunk.ToolCall.Name,
                        status = toolResult.Result.Status.ToString(),
                        payload = toolResult.Result.Payload,
                        blocks = new[]
                        {
                            new
                            {
                                type = "tool_result",
                                tool_call_id = toolResult.ToolCallId,
                                tool_name = chunk.ToolCall.Name,
                                content = toolResult.Result.Payload.GetRawText()
                            }
                        }
                    }));

                    // 将工具结果回灌为后续模型消息，允许 provider 继续推理。
                    messages.Add(ModelMessage.ToolResult(chunk.ToolCall.Id, toolResult.Result.Payload.GetRawText(), chunk.ToolCall.Name));
                }

                if (chunk.StopReason is not null)
                {
                    yield return new WorkerEvent("worker.state.changed", JsonSerializer.SerializeToElement(new { stopReason = chunk.StopReason.ToString() }));
                }

                if (!string.IsNullOrWhiteSpace(chunk.Error))
                {
                    yield return new WorkerEvent("worker.error", JsonSerializer.SerializeToElement(new { message = chunk.Error }));
                }
            }

            if (!hasToolCall)
            {
                if (assistantBuffer.Length > 0)
                {
                    messages.Add(new ModelMessage(ModelMessageRole.Assistant, assistantBuffer.ToString()));
                    assistantBuffer.Clear();
                }

                yield break;
            }
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        _runCancellation?.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _runCancellation?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// 基于 stdio JSON-RPC 的 worker 客户端。
/// </summary>
public sealed class StdioJsonRpcAgentWorkerClient : IAgentWorkerClient
{
    /// <inheritdoc />
    public Task<IAgentWorkerSession> ConnectAsync(Process process, AgentLaunchPlan plan, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAgentWorkerSession>(new StdioAgentWorkerSession(process, plan));
    }
}

internal sealed class StdioAgentWorkerSession : IAgentWorkerSession
{
    private readonly Process _process;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WorkerResponse>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _loopCts = new();
    private readonly Task _receiveLoop;

    public StdioAgentWorkerSession(Process process, AgentLaunchPlan plan)
    {
        _process = process;
        _writer = process.StandardInput;
        _reader = process.StandardOutput;
        Handle = new AgentProcessHandle(process.Id, $"{process.StartInfo.FileName} {process.StartInfo.Arguments}".Trim(), true, plan.Session.Record.SessionId.Value, DateTimeOffset.UtcNow);
        _receiveLoop = Task.Run(ReceiveLoopAsync);
    }

    public AgentProcessHandle Handle { get; }

    public Task InitializeAsync(WorkerInitializeRequest request, CancellationToken cancellationToken = default) =>
        SendRequestAsync("worker.initialize", JsonSerializer.SerializeToElement(request), cancellationToken);

    public async IAsyncEnumerable<WorkerEvent> RunTurnAsync(
        WorkerTurnRequest request,
        Func<WorkerToolRequest, Task<WorkerToolResult>> onToolRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await SendRequestAsync("worker.runTurn", JsonSerializer.SerializeToElement(request), cancellationToken).ConfigureAwait(false);
        yield return new WorkerEvent("worker.ready", JsonSerializer.SerializeToElement(new { sessionId = request.SessionId }));
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) =>
        SendRequestAsync("worker.cancel", JsonSerializer.SerializeToElement(new { }), cancellationToken);

    public Task ShutdownAsync(CancellationToken cancellationToken = default) =>
        SendRequestAsync("worker.shutdown", JsonSerializer.SerializeToElement(new { }), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _loopCts.CancelAsync();
        try
        {
            await _receiveLoop.ConfigureAwait(false);
        }
        catch
        {
            //
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }

    private async Task SendRequestAsync(string method, JsonElement payload, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<WorkerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        var envelope = JsonSerializer.Serialize(new WorkerRpcEnvelope(id, method, payload));
        var bytes = Encoding.UTF8.GetBytes(envelope);
        await _writer.WriteAsync($"Content-Length: {bytes.Length}\r\n\r\n{envelope}".AsMemory(), cancellationToken).ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        var response = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error ?? $"Worker request '{method}' failed.");
        }
    }

    private async Task ReceiveLoopAsync()
    {
        while (!_loopCts.IsCancellationRequested)
        {
            var header = await _reader.ReadLineAsync(_loopCts.Token).ConfigureAwait(false);
            if (header is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var length = int.Parse(header["Content-Length:".Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            await _reader.ReadLineAsync(_loopCts.Token).ConfigureAwait(false);

            var buffer = new char[length];
            var read = 0;
            while (read < length)
            {
                var chunk = await _reader.ReadAsync(buffer.AsMemory(read, length - read), _loopCts.Token).ConfigureAwait(false);
                if (chunk == 0)
                {
                    throw new EndOfStreamException("Worker stream closed unexpectedly.");
                }

                read += chunk;
            }

            var payload = new string(buffer);
            var response = JsonSerializer.Deserialize<WorkerResponse>(payload);
            if (response is null)
            {
                continue;
            }

            if (_pending.TryRemove(response.RequestId, out var completion))
            {
                completion.TrySetResult(response);
            }
        }
    }
}
