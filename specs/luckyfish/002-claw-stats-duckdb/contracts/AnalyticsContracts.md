# 分析服务契约 (Analytics Contracts)

## 1. ISessionAnalyticsService

该服务负责在 DuckDB 上执行分析查询。

### 获取当前快照
```csharp
Task<SessionAnalyticsSnapshot> GetSnapshotAsync(DateTimeOffset? start = null, DateTimeOffset? end = null, CancellationToken ct = default);
```

### 获取 Token 消耗趋势
```csharp
Task<IReadOnlyList<TokenUsageMetric>> GetTokenUsageTrendAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
```

### 获取工具统计
```csharp
Task<IReadOnlyList<ToolUsageMetric>> GetToolUsageStatsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
```

### 获取 Agent 性能指标
```csharp
Task<IReadOnlyList<AgentPerformanceMetric>> GetAgentPerformanceAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
```

## 2. CLI 命令：ClawStatsCommand

`ClawSharp.CLI` 中将增加一个新命令：
- **Name**: `stats`
- **Aliases**: `usage`, `metrics`
- **Options**:
  - `-p|--period`: 时间范围 (24h, 7d, 30d, all)。默认 24h。
  - `--tools`: 仅显示工具统计。
  - `--agents`: 仅显示 Agent 性能。
  - `-f|--format`: 输出格式 (table, json)。默认 table。
