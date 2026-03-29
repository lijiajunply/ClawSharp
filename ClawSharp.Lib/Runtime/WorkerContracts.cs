using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Runtime;

public sealed record WorkerRpcEnvelope(string Id, string Method, JsonElement Payload);

public sealed record WorkerRequest(string RequestId, string Method, JsonElement Payload);

public sealed record WorkerResponse(string RequestId, bool Success, JsonElement Payload, string? Error = null);

public sealed record WorkerEvent(string EventType, JsonElement Payload);

public sealed record WorkerInitializeRequest(string SessionId, string AgentId, string TraceId);

public sealed record WorkerTurnRequest(
    string SessionId,
    string TurnId,
    string TraceId,
    ResolvedModelTarget Target,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ModelToolSchema> Tools,
    string? SystemPrompt);

public sealed record WorkerToolRequest(string ToolCallId, string ToolName, string ArgumentsJson);

public sealed record WorkerToolResult(string ToolCallId, ToolInvocationResult Result);

public sealed record AgentProcessHandle(int? ProcessId, string? CommandLine, bool IsConnected, string? WorkerSessionId, DateTimeOffset StartedAt, DateTimeOffset? ReadyAt = null);

public interface IAgentWorkerSession : IAsyncDisposable
{
    AgentProcessHandle Handle { get; }

    Task InitializeAsync(WorkerInitializeRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<WorkerEvent> RunTurnAsync(WorkerTurnRequest request, Func<WorkerToolRequest, Task<WorkerToolResult>> onToolRequest, CancellationToken cancellationToken = default);

    Task CancelAsync(CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

public interface IAgentWorkerClient
{
    Task<IAgentWorkerSession> ConnectAsync(Process process, AgentLaunchPlan plan, CancellationToken cancellationToken = default);
}

public interface IAgentWorkerLauncher
{
    Task<IAgentWorkerSession> LaunchAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default);
}

public sealed class DefaultAgentWorkerLauncher(
    ClawOptions options,
    IAgentWorkerClient workerClient,
    IModelProviderRegistry providerRegistry) : IAgentWorkerLauncher
{
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

public sealed class LoopbackAgentWorkerSession : IAgentWorkerSession
{
    private readonly AgentLaunchPlan _plan;
    private readonly IModelProvider _provider;
    private CancellationTokenSource? _runCancellation;

    public LoopbackAgentWorkerSession(AgentLaunchPlan plan, IModelProvider provider)
    {
        _plan = plan;
        _provider = provider;
        Handle = new AgentProcessHandle(null, null, true, plan.Session.Record.SessionId.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    public AgentProcessHandle Handle { get; }

    public Task InitializeAsync(WorkerInitializeRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<WorkerEvent> RunTurnAsync(
        WorkerTurnRequest request,
        Func<WorkerToolRequest, Task<WorkerToolResult>> onToolRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var messages = request.Messages.ToList();

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
            await foreach (var chunk in _provider.StreamAsync(modelRequest, _runCancellation.Token).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.TextDelta))
                {
                    yield return new WorkerEvent("worker.message.delta", JsonSerializer.SerializeToElement(new { delta = chunk.TextDelta }));
                }

                if (chunk.ToolCall is not null)
                {
                    hasToolCall = true;
                    yield return new WorkerEvent("worker.tool.requested", JsonSerializer.SerializeToElement(chunk.ToolCall));
                    var toolResult = await onToolRequest(new WorkerToolRequest(chunk.ToolCall.Id, chunk.ToolCall.Name, chunk.ToolCall.ArgumentsJson)).ConfigureAwait(false);
                    yield return new WorkerEvent("worker.tool.completed", JsonSerializer.SerializeToElement(new
                    {
                        toolCallId = toolResult.ToolCallId,
                        status = toolResult.Result.Status.ToString(),
                        payload = toolResult.Result.Payload
                    }));
                    messages.Add(new ModelMessage(ModelMessageRole.Tool, toolResult.Result.Payload.GetRawText(), chunk.ToolCall.Name, chunk.ToolCall.Id));
                }

                if (chunk.StopReason is not null)
                {
                    yield return new WorkerEvent("worker.state.changed", JsonSerializer.SerializeToElement(new { stopReason = chunk.StopReason.ToString() }));
                }
            }

            if (!hasToolCall)
            {
                yield break;
            }
        }
    }

    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        _runCancellation?.Cancel();
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _runCancellation?.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class StdioJsonRpcAgentWorkerClient : IAgentWorkerClient
{
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
        _loopCts.Cancel();
        try
        {
            await _receiveLoop.ConfigureAwait(false);
        }
        catch
        {
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
        await _writer.FlushAsync().ConfigureAwait(false);

        var response = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error ?? $"Worker request '{method}' failed.");
        }
    }

    private async Task ReceiveLoopAsync()
    {
        while (!_loopCts.IsCancellationRequested && !_reader.EndOfStream)
        {
            var header = await _reader.ReadLineAsync(_loopCts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var length = int.Parse(header["Content-Length:".Length..].Trim());
            await _reader.ReadLineAsync(_loopCts.Token).ConfigureAwait(false);
            var buffer = new char[length];
            var read = await _reader.ReadBlockAsync(buffer.AsMemory(0, length), _loopCts.Token).ConfigureAwait(false);
            var payload = new string(buffer, 0, read);
            var response = JsonSerializer.Deserialize<WorkerResponse>(payload);
            if (response is not null && _pending.TryRemove(response.RequestId, out var tcs))
            {
                tcs.TrySetResult(response);
            }
        }
    }
}
