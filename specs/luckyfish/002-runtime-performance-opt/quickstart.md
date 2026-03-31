# Quickstart: Runtime Performance Optimization

## Overview
This feature introduces caching and connection pooling to the ClawSharp kernel to significantly reduce turn latency.

## Key Components

### 1. Agent Launch Plan Cache
`ClawRuntime` now maintains an in-memory cache of `AgentLaunchPlan` objects.
- **Automatic**: The cache is populated on the first turn of a session.
- **Invalidation**: Filesystem changes detected by `DefinitionWatcher` automatically evict stale cache entries.

### 2. MCP Connection Pool
`McpService` maintains a pool of active `McpClient` instances.
- **Handshake Avoidance**: Subsequent turns reuse existing connections, skipping the expensive `InitializeAsync` phase.
- **Idle Timeout**: Connections are closed after 10 minutes of inactivity.

### 3. Manual Reload
A new `/reload` command is available in the CLI to force-refresh all caches.

## Verification
To verify the performance gains:
1. Start a session: `claw chat`
2. Send a message. Note the initial latency (parsing + handshake).
3. Send a second message. Observe the near-instant response (cache hit).
4. Modify the agent's Markdown file.
5. Send a third message. Observe the one-time parsing latency as the cache is refreshed.
