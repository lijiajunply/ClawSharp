# 数据模型：DuckDB 分析实体

## 1. 分析结果记录 (Records)

这些记录用于在 `ISessionAnalyticsService` 和 UI 层之间传递数据。

### TokenUsageMetric
代表 Token 消耗的时序数据或汇总。
- `InputTokens`: int
- `OutputTokens`: int
- `Timestamp`: DateTimeOffset (用于时序图)

### ToolUsageMetric
代表特定工具的统计。
- `ToolName`: string
- `CallCount`: int
- `SuccessCount`: int
- `FailureCount`: int

### AgentPerformanceMetric
代表 Agent 的性能指标。
- `AgentId`: string
- `AvgLatencyMs`: double
- `MinLatencyMs`: double
- `MaxLatencyMs`: double
- `RequestCount`: int

## 2. DuckDB SQL 逻辑 (内嵌于服务)

查询逻辑将基于以下 DuckDB 表：

- **`sessions`**: 关联 Agent ID。
- **`message_blocks`**: 统计 `block_type = 'tool_use'` 的工具调用。
- **`session_events`**: 
  - `event_type = 'TurnCompleted'` -> 解析 `payload` 获取 `Usage` 和耗时。
  - `event_type = 'ToolCallCompleted'` -> 解析 `payload` 获取工具执行结果和耗时。

### 示例视图定义 (查询逻辑中):
```sql
CREATE VIEW v_usage AS
SELECT 
    session_id,
    created_at,
    (json_extract(payload, '$.Usage.PromptTokens')::INT) as input_tokens,
    (json_extract(payload, '$.Usage.CompletionTokens')::INT) as output_tokens,
    (json_extract(payload, '$.LatencyMs')::DOUBLE) as latency_ms
FROM session_events
WHERE event_type = 'TurnCompleted';
```

## 3. 验证规则
- `CallCount` 必须等于 `SuccessCount + FailureCount`。
- `AvgLatencyMs` 必须是非负数。
- 如果没有数据，返回空列表而非 null。
