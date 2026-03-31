# Tasks: Runtime Performance Optimization

**Input**: Design documents from `/specs/luckyfish/002-runtime-performance-opt/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 [P] Extend `PerformanceMetrics` schema in `ClawSharp.Lib/Runtime/AnalyticsServices.cs` to include cache/pooling fields
- [ ] T002 [P] Add `McpPoolOptions` to `ClawSharp.Lib/Configuration/ClawOptions.cs` (TTL, MaxPoolSize)

## Phase 2: Foundational (Blocking Prerequisites)

- [ ] T003 Define `AgentLaunchPlanCacheEntry` in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`
- [ ] T004 Define `McpClientPoolEntry` in `ClawSharp.Lib/Mcp/McpContracts.cs`
- [ ] T005 [P] Add `ReloadAsync` method signature to `IClawRuntime` in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`
- [ ] T006 [P] Add `ReleaseAsync` method signature to `IMcpClientManager` in `ClawSharp.Lib/Mcp/McpContracts.cs`

## Phase 3: User Story 1 - 启动加速 (Priority: P1) 🎯 MVP

**Goal**: Cache `AgentLaunchPlan` in `ClawRuntime` to reduce turn latency.

**Independent Test**: Run a session, send two messages; second message must show log "Cache hit for AgentLaunchPlan".

- [ ] T007 [US1] Implement `ConcurrentDictionary` based cache in `ClawSharp.Lib/Runtime/ClawRuntime.cs`
- [ ] T008 [US1] Update `PrepareAgentAsync` in `ClawSharp.Lib/Runtime/ClawRuntime.cs` to check cache before parsing
- [ ] T009 [US1] Subscribe `ClawRuntime` to `DefinitionWatcher` events to evict cache on file change
- [ ] T010 [US1] Add unit tests for `AgentLaunchPlan` cache hit/miss in `ClawSharp.Lib.Tests/RuntimeIntegrationTests.cs`
- [ ] T010b [US1] Verify cache isolation between different AgentIds in `ClawSharp.Lib.Tests/RuntimeIntegrationTests.cs`

## Phase 4: User Story 2 - MCP 长连接 (Priority: P1)

**Goal**: Reuse MCP connections across turns using a connection pool.

**Independent Test**: Call an MCP tool twice; second call must skip "Initializing MCP client" log and finish < 50ms.

- [ ] T011 [US2] Implement `McpClientPool` logic in `ClawSharp.Lib/Mcp/McpService.cs`
- [ ] T012 [US2] Update `ConnectAsync` in `ClawSharp.Lib/Mcp/McpService.cs` to fetch from pool (including fallback for faulted clients)
- [ ] T013 [US2] Implement `ReleaseAsync` in `ClawSharp.Lib/Mcp/McpService.cs` to return client to pool
- [ ] T014 [US2] Add integration test for MCP connection reuse in `ClawSharp.Lib.Tests/McpIntegrationTests.cs`

## Phase 5: User Story 3 - 资源回收 & 手动刷新 (Priority: P2)

**Goal**: Automatically cleanup idle connections and provide manual reload command.

**Independent Test**: Set TTL to 1s, wait, verify connection closed; Run `/reload` and verify all caches cleared.

- [ ] T015 [US3] Implement background cleanup timer for idle connections in `ClawSharp.Lib/Mcp/McpService.cs`
- [ ] T016 [US3] Implement `ReloadAsync` in `ClawSharp.Lib/Runtime/ClawRuntime.cs` to clear all internal caches
- [ ] T017 [US3] Implement `ReloadCommand` in `ClawSharp.CLI/Commands/SpecKitCommands.cs` to expose `/reload`
- [ ] T018 [US3] Add unit tests for cache eviction and TTL cleanup in `ClawSharp.Lib.Tests/RuntimeIntegrationTests.cs`

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T019 [P] Update `ClawSharp.CLI` UI to display cache hit/miss status in turn summary (optional polish)
- [ ] T020 [P] Final code review and refactor to ensure Principle III (Async-First) compliance
- [ ] T021 Run `specs/luckyfish/002-runtime-performance-opt/quickstart.md` validation scenarios

## Dependencies & Execution Order

1. **Foundational (Phase 2)** must be completed before US1 or US2 can start.
2. **US1 (Phase 3)** and **US2 (Phase 4)** are independent and can be implemented in parallel.
3. **US3 (Phase 5)** depends on US1 and US2 being mostly functional.
4. **Polish (Phase 6)** requires all user stories to be complete.

## Parallel Execution Examples

```bash
# Implement core entities in parallel (after Phase 1):
Task T003: Define AgentLaunchPlanCacheEntry
Task T004: Define McpClientPoolEntry

# Implement US1 and US2 in parallel:
Task T007-T009: ClawRuntime caching logic
Task T011-T013: McpService pooling logic
```

## Implementation Strategy

1. **MVP**: Complete Phase 1-3. This delivers the most significant turn latency reduction for standard chat.
2. **Incremental**: Add Phase 4 for tool-heavy users.
3. **Stability**: Add Phase 5 for long-running sessions and reliability.
