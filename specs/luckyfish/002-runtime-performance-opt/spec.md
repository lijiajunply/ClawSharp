# Feature Specification: Runtime Performance Optimization

**Feature Branch**: `luckyfish/002-runtime-performance-opt`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "运行时性能 (Runtime Performance) * 启动计划缓存: PrepareAgentAsync 目前在每轮 turn 都会重新解析定义。建议对活跃 Session 缓存 AgentLaunchPlan，仅在 DefinitionWatcher 检测到文件变更时才刷新。 * MCP 连接池: 目前每轮都会尝试 ConnectAsync。应建立长连接池管理，减少 Handshake 的延迟。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 启动加速 (Priority: P1)

作为一名开发者，当我与活跃的 Session 进行多轮对话时，我希望 AI 的回复能更快响应，因为系统不再重复解析 Agent 定义文件（AgentDefinition），而是直接从内存中读取已解析的启动计划（AgentLaunchPlan）。

**Why this priority**: 核心性能需求，直接影响用户体验的流畅度。

**Independent Test**: 通过多次交互（多轮 Turn），观察 AI 响应延迟。第二次及以后的 Turn 应显著快于第一次（因为缓存已生效）。

**Acceptance Scenarios**:

1. **Given** 活跃的 Session, **When** 进行第二次对话 Turn, **Then** `PrepareAgentAsync` 直接从缓存中获取 `AgentLaunchPlan` 而不重新解析 Markdown。
2. **Given** 缓存生效中, **When** `DefinitionWatcher` 检测到 Agent 定义文件变更, **Then** 该 Session 的缓存应失效。

---

### User Story 2 - MCP 长连接 (Priority: P1)

作为一名使用 MCP 工具的用户，我希望在工具调用时几乎感觉不到建立连接的过程，因为系统通过连接池维护着与 MCP Server 的长连接，无需每轮对话都进行握手（Handshake）。

**Why this priority**: MCP 握手通常包含协议版本协商和能力列举，延迟明显。长连接是工具系统可用性的基础。

**Independent Test**: 连续调用两次 MCP 工具，第二次调用不应触发完整的 `ConnectAsync` 握手序列。

**Acceptance Scenarios**:

1. **Given** 已建立的 MCP 连接, **When** 工具调用结束, **Then** 连接不被关闭，而是返回池中。
2. **Given** 活跃连接池, **When** 进行下一次工具调用, **Then** 系统复用现有连接而不是重新握手。

---

### User Story 3 - 资源回收 (Priority: P2)

作为一名系统管理员，我希望系统在不使用 Agent 或 MCP 连接时，能自动释放这些内存和网络资源，避免长时间运行导致的性能下降。

**Why this priority**: 生产环境稳定性需求，防止资源泄露。

**Independent Test**: 停止所有对话 Session 并等待超时时间，观察内存占用和网络连接数。

**Acceptance Scenarios**:

1. **Given** 连接池中的空闲连接, **When** 超过定义的超时时间（默认 10 分钟）, **Then** 连接应自动断开并移除。
2. **Given** 已关闭的 Session, **When** 释放资源时, **Then** 对应的 `AgentLaunchPlan` 缓存应被清理。

---

### Edge Cases

- 当 MCP Server 崩溃或意外断开时，系统如何处理？（应检测心跳并从池中移除，必要时重新连接）
- 如果同时修改了多个 Agent 定义文件，缓存刷新是否会导致明显的并发延迟？
- 在极低并发环境下，长连接池是否会有资源空闲策略？系统将采用固定超时（Fixed Timeout）策略，即空闲连接在 10 分钟后自动回收。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系统必须为活跃 Session 实现 `AgentLaunchPlan` 缓存机制。
- **FR-002**: 系统必须集成 `DefinitionWatcher`，当 Markdown 定义文件变更时主动刷新对应缓存。
- **FR-003**: 系统必须实现 MCP 连接池，支持基于 `McpStdioTransport` 的长连接复用。
- **FR-004**: 连接池必须支持配置连接超时策略。默认空闲超时时间（TTL）为 10 分钟。
- **FR-005**: 当从池中获取连接失败时，系统必须具备优雅的回退机制（如尝试重新建立连接或抛出具体错误）。
- **FR-006**: 缓存系统必须具备隔离性，不同 Session 或不同版本的 Agent 必须能正确识别并更新。
- **FR-007**: 系统必须支持通过手动指令（如 `/reload`）强制刷新 Agent 缓存和重置 MCP 连接池，以应对文件系统监听失效的情况。

### Key Entities *(include if feature involves data)*

- **AgentLaunchPlan**: 已解析的 Agent 定义、绑定的工具和执行计划的内存表示。
- **McpConnectionPool**: 负责存储和分配 `McpClient` 实例，管理其生命周期和健康检查。
- **DefinitionWatcher**: 核心监听组件，捕获本地文件系统中的变更事件并分发。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 在单次 Session 中，第二次对话 Turn 的解析延迟（Parsing Latency）降低 90% 以上。
- **SC-002**: 连续调用 MCP 工具时，第二次工具调用的连接建立延迟（Handshake Latency）降低到 50ms 以内（在本地标准环境下）。
- **SC-003**: 长时间空闲后，资源占用恢复至基准水平，且无残留的死进程或泄露连接。
- **SC-004**: 定义文件被修改后，后续第一次 Turn 的解析结果必须是 100% 最新的（一致性验证）。

## Assumptions

- 假定用户在本地环境运行，文件系统变更通知（FileSystemWatcher）是可靠的。
- 假定 MCP Server 支持长连接且能在空闲时保持稳定。
- 假定 `DefinitionWatcher` 已经能够正确识别项目中的 `.md` 文件路径变更。
