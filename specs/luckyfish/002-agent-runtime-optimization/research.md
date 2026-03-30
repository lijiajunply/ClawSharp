# Research: Agent Runtime Strategy Optimization

## Topic 1: JIT Permission Prompt Architecture

- **Decision**: Introduce `IPermissionUI` interface in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`.
- **Rationale**: Keeps the library layer UI-agnostic while allowing the CLI or Desktop app to provide custom interactive prompts.
- **Alternatives Considered**: 
    - Hard-coding CLI calls: Rejected (Violates "Library-First" and decoupling).
    - Polling: Rejected (Too slow and complex for simple permission prompts).
- **Implementation Strategy**:
    ```csharp
    public interface IPermissionUI {
        Task<bool> RequestCapabilityAsync(string agentId, ToolCapability capability, string toolName, CancellationToken ct);
    }
    ```

## Topic 2: Mandatory Tool Injection

- **Decision**: Update `ClawOptions.cs` to include `List<string> MandatoryTools` and `WorkspacePolicy`.
- **Rationale**: Configuration-driven approach allows administrators to enforce logging/telemetry without editing every `agent.md`.
- **Logic**: During `PrepareAgentAsync`, the `MandatoryTools` names are added to the `plan.Tools` list. If any injected tool requires capabilities missing from the current `EffectivePermissionSet`, the `PermissionResolver` marks them as "Needs Elevation".

## Topic 3: Permission Resolver Internal Logic

- **Decision**: Create `PermissionResolver` service.
- **Rationale**: Consolidates bitmask arithmetic and path root intersections in one testable place.
- **Algorithm**:
    1. Base = `Agent.Permissions`.
    2. Policy = `Kernel.Options.WorkspacePolicy`.
    3. Intersection = `Base.Capabilities & Policy.Capabilities`.
    4. Path Intersection = Intersection of `AllowedReadRoots`, `AllowedWriteRoots`.
    5. Audit log any capability bits dropped during intersection.
    6. Return `EffectivePermissionSet`.

## Topic 4: Audit and Persistence

- **Decision**: Use `ISessionEventStore` with new event types.
- **Rationale**: Leverages existing robust event-sourced history system.
- **Event Types**:
    - `PermissionPolicyApplied`: Initial set calculated.
    - `JITApprovalRequested`: Prompt shown to user.
    - `JITApprovalGranted/Denied`: Result of user action.
