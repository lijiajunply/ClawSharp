namespace ClawSharp.Lib.Agents;

using System.Text.Json;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wraps an IAgent as an IToolExecutor to allow delegation.
/// </summary>
public sealed class AgentTool(IAgent agent, IServiceProvider serviceProvider) : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        agent.Definition.Name,
        agent.Definition.Description,
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The task or query for the agent." }
            },
            required = new[] { "query" }
        }),
        null,
        ToolCapability.None);

    /// <inheritdoc />
    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        if (!arguments.TryGetProperty("query", out var queryElement))
        {
            return ToolInvocationResult.Failure(Definition.Name, "Missing required argument: query");
        }

        var query = queryElement.GetString() ?? string.Empty;

        try
        {
            var runtime = serviceProvider.GetRequiredService<IClawRuntime>();
            var kernel = serviceProvider.GetRequiredService<IClawKernel>();

            // Record delegation start
            await kernel.Events.AppendAsync(
                new SessionId(context.SessionId),
                new TurnId(context.TurnId ?? string.Empty),
                "DelegationStarted",
                JsonSerializer.SerializeToElement(new { caller = context.AgentId, callee = agent.Definition.Id, query }),
                context.CancellationToken).ConfigureAwait(false);

            // Start a new session for the sub-agent.
            var subSession = await runtime.StartSessionAsync(agent.Definition.Id, context.CancellationToken).ConfigureAwait(false);

            // Add the query as a user message.
            await runtime.AppendUserMessageAsync(subSession.Record.SessionId, query, context.CancellationToken).ConfigureAwait(false);

            // Prepare delegation context for the sub-agent.
            var subDelegation = new DelegationContext
            {
                CallStack = new List<string>(context.Delegation?.CallStack ?? new List<string>()) { context.AgentId },
                CorrelationId = context.Delegation?.CorrelationId ?? Guid.NewGuid(),
                Permissions = context.Delegation?.Permissions // Inherit for now
            };

            // Run the turn.
            var result = await runtime.RunTurnAsync(subSession.Record.SessionId, subDelegation, context.CancellationToken).ConfigureAwait(false);

            // Record delegation completion
            await kernel.Events.AppendAsync(
                new SessionId(context.SessionId),
                new TurnId(context.TurnId ?? string.Empty),
                "DelegationCompleted",
                JsonSerializer.SerializeToElement(new { caller = context.AgentId, callee = agent.Definition.Id, status = result.Status.ToString() }),
                context.CancellationToken).ConfigureAwait(false);

            // Return the assistant's response.
            return ToolInvocationResult.Success(Definition.Name, JsonSerializer.SerializeToElement(new
            {
                agent = agent.Definition.Name,
                response = result.AssistantMessage
            }));
        }
        catch (Exception ex)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"Delegation to agent '{agent.Definition.Name}' failed: {ex.Message}");
        }
    }
}
