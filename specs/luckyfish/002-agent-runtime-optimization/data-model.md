# Data Model: Agent Runtime Strategy Optimization

## Modified Entities

### ToolPermissionSet (Existing)
Updates to logic:
- `Capabilities`: Now represented as `EffectiveCapabilities` (calculated result).
- `IsJitElevated`: Boolean to track if current capability mask contains JIT-granted bits.
- `AuditLog`: New field or sidecar entity to track why certain permissions were granted or denied.

### SessionEvent (Existing)
New `EventType` values:
- `PermissionPolicyApplied`: `{ "calculated_mask": 255, "denied_mask": 1024 }`
- `JitElevationRequested`: `{ "capability": "Shell.Execute", "tool": "shell_run" }`
- `JitElevationGranted`: `{ "capability": "Shell.Execute", "granted_at": "2026-03-30T10:00:00Z" }`
- `JitElevationDenied`: `{ "capability": "Shell.Execute", "reason": "User refused" }`

## New Logic Concepts

### WorkspacePolicy
(Stored in `ClawOptions`)
- `MaxCapabilities`: `ToolCapability` bitmask (The ceiling for ALL Agents).
- `MandatoryTools`: `List<string>` of tools names to be injected regardless of agent def.
- `RestrictedReadRoots`: Paths that MUST NOT be read.
- `RestrictedWriteRoots`: Paths that MUST NOT be written.

### PermissionRequest
(Ephemeral, In-Memory)
- `RequestId`: `Guid`
- `AgentId`: `string`
- `Capability`: `ToolCapability`
- `ToolName`: `string`
- `Status`: `Pending | Approved | Denied`
- `RequestedAt`: `DateTimeOffset`
