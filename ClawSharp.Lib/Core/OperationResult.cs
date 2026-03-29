namespace ClawSharp.Lib.Core;

/// <summary>
/// 表示不返回值的操作结果。
/// </summary>
/// <param name="IsSuccess">操作是否成功。</param>
/// <param name="Error">操作失败时的错误描述。</param>
public sealed record OperationResult(bool IsSuccess, string? Error = null)
{
    /// <summary>
    /// 创建一个成功结果。
    /// </summary>
    /// <returns>表示成功的 <see cref="OperationResult"/>。</returns>
    public static OperationResult Success() => new(true);

    /// <summary>
    /// 创建一个失败结果。
    /// </summary>
    /// <param name="error">失败原因。</param>
    /// <returns>表示失败的 <see cref="OperationResult"/>。</returns>
    public static OperationResult Failure(string error) => new(false, error);
}

/// <summary>
/// 表示包含返回值的操作结果。
/// </summary>
/// <typeparam name="T">成功时携带的值类型。</typeparam>
/// <param name="IsSuccess">操作是否成功。</param>
/// <param name="Value">成功时返回的值。</param>
/// <param name="Error">操作失败时的错误描述。</param>
public sealed record OperationResult<T>(bool IsSuccess, T? Value = default, string? Error = null)
{
    /// <summary>
    /// 创建一个携带返回值的成功结果。
    /// </summary>
    /// <param name="value">成功结果携带的值。</param>
    /// <returns>表示成功的 <see cref="OperationResult{T}"/>。</returns>
    public static OperationResult<T> Success(T value) => new(true, value);

    /// <summary>
    /// 创建一个失败结果。
    /// </summary>
    /// <param name="error">失败原因。</param>
    /// <returns>表示失败的 <see cref="OperationResult{T}"/>。</returns>
    public static OperationResult<T> Failure(string error) => new(false, default, error);
}
