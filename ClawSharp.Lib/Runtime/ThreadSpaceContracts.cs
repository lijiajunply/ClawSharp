using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// ThreadSpace 标识值对象。
/// </summary>
/// <param name="Value">ThreadSpace 的字符串值。</param>
public readonly record struct ThreadSpaceId(string Value)
{
    /// <summary>
    /// 返回底层字符串值。
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// 创建一个新的 ThreadSpace 标识。
    /// </summary>
    public static ThreadSpaceId New() => new(Guid.NewGuid().ToString("N"));
}

/// <summary>
/// 描述一个绑定目录的聊天/工作容器。
/// </summary>
public sealed record ThreadSpaceRecord(
    ThreadSpaceId ThreadSpaceId,
    string Name,
    string BoundFolderPath,
    bool IsInit,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt = null);

/// <summary>
/// 描述一次 ThreadSpace 创建请求。
/// </summary>
public sealed record CreateThreadSpaceRequest(string Name, string BoundFolderPath)
{
    /// <summary>
    /// 校验请求是否包含必要字段。
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(BoundFolderPath))
        {
            throw new ValidationException("ThreadSpace creation request is missing one or more required fields.");
        }
    }
}

/// <summary>
/// ThreadSpace 持久化存储抽象。
/// </summary>
public interface IThreadSpaceStore
{
    /// <summary>
    /// 创建一个新的 ThreadSpace 记录。
    /// </summary>
    Task CreateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标识读取 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord?> GetAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按名称读取 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按绑定目录读取 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord?> GetByBoundFolderPathAsync(string boundFolderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出 ThreadSpace。
    /// </summary>
    Task<IReadOnlyList<ThreadSpaceRecord>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// ThreadSpace 生命周期与约束管理抽象。
/// </summary>
public interface IThreadSpaceManager
{
    /// <summary>
    /// 确保默认 <c>init</c> ThreadSpace 存在。
    /// </summary>
    Task<ThreadSpaceRecord> EnsureDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取默认 <c>init</c> ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord> GetInitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建一个新的 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord> CreateAsync(CreateThreadSpaceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标识读取 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord> GetAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按名称读取 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按绑定目录读取 ThreadSpace。
    /// </summary>
    Task<ThreadSpaceRecord?> GetByBoundFolderPathAsync(string boundFolderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出 ThreadSpace。
    /// </summary>
    Task<IReadOnlyList<ThreadSpaceRecord>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出某个 ThreadSpace 下的 session。
    /// </summary>
    Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default);
}
