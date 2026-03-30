# Quickstart: Agent Runtime Strategy Optimization

## How to test the JIT permission flow

1. **Configure a Mandatory Tool**:
   Update `appsettings.Local.json` to include a tool that requires `Shell.Execute` in the `MandatoryTools` list.
   ```json
   {
     "ClawSharp": {
       "WorkspacePolicy": {
         "Capabilities": "FileRead, NetworkAccess",
         "MandatoryTools": ["shell_run"]
       }
     }
   }
   ```

2. **Run an Agent without that capability**:
   Create a simple `agent.md` with only `FileRead` capability.

3. **Observe the JIT Prompt**:
   Start a chat with that agent: `claw chat my-agent`.
   When the session starts, the `ClawRuntime` will detect that `shell_run` (a mandatory tool) needs `Shell.Execute`.
   The `CliPermissionUI` should trigger a Spectre.Console prompt:
   ```text
   [bold yellow]PERMISSION REQUEST[/]
   Agent 'my-agent' is attempting to use 'shell_run' which requires 'Shell.Execute'.
   This capability is not currently granted to the agent.
   
   Do you want to grant this capability for the current session? (y/n)
   ```

4. **Audit results**:
   Check the session history or analytics: `claw history <session-id>`.
   You should see events for `PermissionElevationRequested` and `PermissionElevationApproved`.

## Verification Scenarios

| Scenario | Expected Result |
|----------|-----------------|
| Agent has `Shell.Execute`, Policy has `None` | `Shell.Execute` is DENIED at start. |
| Mandatory tool injected, Capability missing | JIT Prompt shown. |
| User denies JIT | Tool invocation fails with "Permission Denied". |
| Policy changes at runtime | Next session reflects new policy. |
