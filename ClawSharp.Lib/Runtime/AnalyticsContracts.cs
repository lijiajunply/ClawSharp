namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 会话分析服务抽象。
/// </summary>
public interface ISessionAnalyticsService
{
    /// <summary>
    /// 获取当前运行时数据的分析摘要。
    /// </summary>
    /// <param name="start">可选开始时间。</param>
    /// <param name="end">可选结束时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>聚合后的分析结果。</returns>
    Task<SessionAnalyticsSnapshot> GetSnapshotAsync(
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Token 消耗趋势。
    /// </summary>
    Task<IReadOnlyList<TokenUsageMetric>> GetTokenUsageTrendAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取工具调用统计。
    /// </summary>
    Task<IReadOnlyList<ToolUsageMetric>> GetToolUsageStatsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Agent 性能指标。
    /// </summary>
    Task<IReadOnlyList<AgentPerformanceMetric>> GetAgentPerformanceAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 运行时数据的聚合分析结果。
/// </summary>
public sealed record SessionAnalyticsSnapshot(
    int TotalSessions,
    int ActiveSessions,
    IReadOnlyList<SessionStatusCount> SessionsByStatus,
    IReadOnlyList<PromptRoleCount> MessagesByRole,
    IReadOnlyList<EventTypeCount> EventsByType,
    IReadOnlyList<SessionMessageCount> MessagesPerSession,
    IReadOnlyList<PromptBlockTypeCount> BlocksByType,
    IReadOnlyList<RoleBlockTypeCount> BlocksByRoleAndType);

/// <summary>
/// 按状态汇总的 session 数量。
/// </summary>
public sealed record SessionStatusCount(SessionStatus Status, int Count);

/// <summary>
/// 按角色汇总的消息数量。
/// </summary>
public sealed record PromptRoleCount(PromptMessageRole Role, int Count);

/// <summary>
/// 按事件类型汇总的事件数量。
/// </summary>
public sealed record EventTypeCount(string EventType, int Count);

/// <summary>
/// 每个 session 的消息数量。
/// </summary>
public sealed record SessionMessageCount(SessionId SessionId, int Count);

/// <summary>
/// 按 block 类型汇总的内容块数量。
/// </summary>
public sealed record PromptBlockTypeCount(string BlockType, int Count);

/// <summary>
/// 按消息角色和 block 类型汇总的数量。
/// </summary>
public sealed record RoleBlockTypeCount(PromptMessageRole Role, string BlockType, int Count);

/// <summary>
/// Token 消耗记录。
/// </summary>
public sealed record TokenUsageMetric(int InputTokens, int OutputTokens, DateTimeOffset Timestamp);

/// <summary>
/// 工具使用统计。
/// </summary>
public sealed record ToolUsageMetric(string ToolName, int CallCount, int SuccessCount, int FailureCount);

/// <summary>
/// Agent 性能指标。
/// </summary>
public sealed record AgentPerformanceMetric(string AgentId, double AvgLatencyMs, double MinLatencyMs, double MaxLatencyMs, int RequestCount);
