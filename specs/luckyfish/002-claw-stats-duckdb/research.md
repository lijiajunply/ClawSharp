# 研究报告：DuckDB 分析层落地

## 1. 现状分析
ClawSharp 目前已经有一个 `IDuckDbAnalyticsProjector`，它负责将数据从 SQLite 同步到 DuckDB。目前的同步逻辑涵盖了 `sessions`、`messages`、`message_blocks` 和 `session_events` 表。

### 关键数据源：
- **Token 消耗**: 存储在 `session_events` 中，类型为 `TurnCompleted`。其 `payload` JSON 包含 `Usage` 对象（`InputTokens`, `OutputTokens`）。
- **工具调用**: 存储在 `message_blocks` 中，类型为 `tool_use`。可以通过 `name` 和 `arguments_json` 统计。
- **响应时长**: 存储在 `session_events` 中，类型为 `TurnCompleted` 或 `ToolCallCompleted`。`payload` 中通常包含时间戳或延迟信息。

## 2. 技术决策

### 决策 1: 扩展 `ISessionAnalyticsService`
**决定**: 在 `ClawSharp.Lib` 中扩展该接口，而不是直接在 CLI 中编写 SQL。
**理由**: 符合“Library-First”原则。所有分析逻辑应可供 Desktop 应用复用。

### 决策 2: 使用 DuckDB JSON 扩展进行查询
**决定**: 利用 DuckDB 强大的 JSON 解析能力直接在 SQL 中提取数据。
**理由**: 性能优于在 C# 中手动解析数千个 JSON 字符串。
**SQL 示例**:
```sql
SELECT 
  json_extract_path(payload, '$.Usage.InputTokens') as input_tokens,
  json_extract_path(payload, '$.Usage.OutputTokens') as output_tokens
FROM session_events 
WHERE event_type = 'TurnCompleted';
```

### 决策 3: CLI 展示框架
**决定**: 使用 `Spectre.Console` 的 `Table`、`BarChart` 和 `Panel`。
**理由**: 现有的 CLI (`ClawSharp.CLI`) 已经集成了 `Spectre.Console`，这能提供一致且美观的用户体验。

## 3. 备选方案评估
- **方案 A (EF Core GroupBy)**: 无法在 DuckDB 上运行（目前主要通过 Stdio 交互，EF Core 驱动支持有限）。
- **方案 B (DuckDB View)**: 在投影过程中创建视图。这会增加投影代码的复杂度。
- **方案 C (直接查询)**: 在 `ISessionAnalyticsService` 中编写原始 DuckDB SQL。这是目前最灵活且性能最高的方式。**已选定此方案。**

## 4. 未解决问题 (NEEDS CLARIFICATION)
- **费用估算**: 是否需要支持配置不同模型的 Token 单价以计算成本？
  - *初步决定*: v1 仅显示 Token 数量，暂不涉及多币种/复杂单价逻辑。
- **多 ThreadSpace 分析**: `claw stats` 是仅针对当前 ThreadSpace 还是全局？
  - *初步决定*: 基于 `ClawOptions` 中配置的 DuckDB 路径，通常是每个 ThreadSpace 一个。
