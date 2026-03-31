namespace ClawSharp.Lib.Agents;

using ClawSharp.Lib.Runtime;

/// <summary>
/// Specialized agent that can orchestrate tasks by delegating to other agents.
/// </summary>
public sealed class SupervisorAgent(AgentDefinition definition, IClawRuntime runtime) 
    : Agent(definition), IOrchestratorAgent
{
    /// <inheritdoc />
    public async Task<Plan> GeneratePlanAsync(string userTask, TurnContext context)
    {
        // TODO: Implement actual planning logic (e.g., using a planning LLM call).
        // For now, we rely on the LLM's intrinsic planning ability via tool calls.
        return new Plan(new List<PlanStep>());
    }

    /// <inheritdoc />
    public async Task<RunTurnResult> ExecutePlanAsync(Plan plan, TurnContext context)
    {
        // This is a high-level execution method. 
        // For User Story 1, the supervisor executes a multi-step process.
        
        // Prepare delegation context for tracking.
        var delegation = new DelegationContext
        {
            CallStack = new List<string>(context.DelegationStack.CallStack) { Definition.Id },
            CorrelationId = context.DelegationStack.CorrelationId,
            Permissions = context.Permissions
        };

        // Execute the turn via the runtime.
        // The runtime will handle AgentTool calls which perform the actual delegation.
        return await runtime.RunTurnAsync(context.SessionId, delegation, context.CancellationToken).ConfigureAwait(false);
    }
}
