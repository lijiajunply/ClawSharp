using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClawSharp.Lib.Mcp;

/// <summary>
/// Common JSON-RPC 2.0 message fields.
/// </summary>
public abstract record McpMessage
{
    /// <summary>
    /// JSON-RPC 版本。默认为 "2.0"。
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = "2.0";
}

/// <summary>
/// Represents a JSON-RPC request or notification.
/// </summary>
public sealed record McpRequest : McpMessage
{
    /// <summary>
    /// 请求 ID。对于通知，该值为 null。
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Id { get; init; }

    /// <summary>
    /// 要调用的方法名。
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// 方法参数。
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}

/// <summary>
/// Represents a JSON-RPC response.
/// </summary>
public sealed record McpResponse : McpMessage
{
    /// <summary>
    /// 对应的请求 ID。
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }

    /// <summary>
    /// 成功时的结果对象。
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    /// <summary>
    /// 失败时的错误对象。
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; init; }
}

/// <summary>
/// Represents a JSON-RPC error object.
/// </summary>
public sealed record McpError
{
    /// <summary>
    /// 错误代码。
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>
    /// 错误消息简述。
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 额外错误数据。
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}
