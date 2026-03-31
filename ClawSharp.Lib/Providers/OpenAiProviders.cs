using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;

namespace ClawSharp.Lib.Providers;

/// <summary>
/// 根据 provider 目标创建 HTTP 客户端的抽象。
/// </summary>
public interface IProviderHttpClientFactory
{
    /// <summary>
    /// 为指定 provider 目标创建一个 HTTP 客户端。
    /// </summary>
    /// <param name="target">已解析的 provider 目标。</param>
    /// <returns>配置好基础地址的 <see cref="HttpClient"/>。</returns>
    HttpClient CreateClient(ResolvedModelTarget target);
}

/// <summary>
/// 默认的 HTTP 客户端工厂。
/// </summary>
public sealed class DefaultProviderHttpClientFactory : IProviderHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient(ResolvedModelTarget target)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(target.BaseUrl.EndsWith('/') ? target.BaseUrl : $"{target.BaseUrl}/", UriKind.Absolute)
        };
    }
}

internal static class OpenAiRequestMapper
{
    public static JsonObject CreateResponsesPayload(ModelRequest request)
    {
        var payload = new JsonObject
        {
            ["model"] = request.Target.Model,
            ["stream"] = true
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["instructions"] = request.SystemPrompt;
        }

        if (request.Temperature is not null)
        {
            payload["temperature"] = request.Temperature.Value;
        }

        if (request.MaxTokens is not null)
        {
            payload["max_output_tokens"] = request.MaxTokens.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.ToolChoice))
        {
            payload["tool_choice"] = request.ToolChoice;
        }

        if (request.Metadata is not null)
        {
            payload["metadata"] = JsonSerializer.SerializeToNode(request.Metadata);
        }

        payload["input"] = new JsonArray(request.Messages.Select(ToResponsesInput).ToArray());
        if (request.Tools.Count > 0)
        {
            payload["tools"] = new JsonArray(request.Tools.Select(ToResponsesTool).ToArray());
        }

        return payload;
    }

    public static JsonObject CreateCompatibleChatPayload(ModelRequest request)
    {
        var payload = new JsonObject
        {
            ["model"] = request.Target.Model,
            ["stream"] = true,
            ["stream_options"] = new JsonObject
            {
                ["include_usage"] = true
            }
        };

        if (request.Temperature is not null)
        {
            payload["temperature"] = request.Temperature.Value;
        }

        if (request.MaxTokens is not null)
        {
            payload["max_tokens"] = request.MaxTokens.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.ToolChoice))
        {
            payload["tool_choice"] = request.ToolChoice;
        }

        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            messages.Add(ToCompatibleMessage(message));
        }

        payload["messages"] = messages;

        if (request.Tools.Count > 0)
        {
            payload["tools"] = new JsonArray(request.Tools.Select(ToCompatibleTool).ToArray());
        }

        return payload;
    }

    private static JsonNode ToResponsesInput(ModelMessage message)
    {
        if (message.ToolResultBlock is { } toolResult)
        {
            return new JsonObject
            {
                ["type"] = "function_call_output",
                ["call_id"] = toolResult.ToolCallId,
                ["output"] = toolResult.Content
            };
        }

        var isAssistant = message.Role == ModelMessageRole.Assistant;
        var contentItems = new JsonArray();
        
        // 分离工具调用块
        var toolUse = message.Blocks.OfType<ModelToolUseBlock>().FirstOrDefault();

        foreach (var block in message.Blocks)
        {
            if (block is ModelTextBlock textBlock)
            {
                contentItems.Add(new JsonObject
                {
                    ["type"] = isAssistant ? "output_text" : "input_text",
                    ["text"] = textBlock.Text
                });
            }
        }

        var node = new JsonObject
        {
            ["role"] = message.Role switch
            {
                ModelMessageRole.System => "system",
                ModelMessageRole.User => "user",
                ModelMessageRole.Assistant => "assistant",
                _ => "user"
            },
            ["content"] = contentItems
        };

        // 如果是 Assistant 且有工具调用
        if (isAssistant && toolUse != null)
        {
            contentItems.Add(new JsonObject
            {
                ["type"] = "function_call",
                ["call_id"] = toolUse.Id,
                ["name"] = toolUse.Name,
                ["arguments"] = toolUse.ArgumentsJson
            });
        }

        return node;
    }

    private static JsonNode ToResponsesTool(ModelToolSchema tool)
    {
        JsonNode parameters;
        if (string.IsNullOrWhiteSpace(tool.JsonSchema))
        {
            parameters = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject()
            };
        }
        else
        {
            parameters = JsonNode.Parse(tool.JsonSchema) ?? new JsonObject();
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["parameters"] = parameters
        };
    }

    private static JsonNode ToCompatibleMessage(ModelMessage message)
    {
        if (message.ToolResultBlock is { } toolResultBlock)
        {
            var toolNode = new JsonObject
            {
                ["role"] = "tool",
                ["content"] = toolResultBlock.Content,
                ["tool_call_id"] = toolResultBlock.ToolCallId
            };

            if (!string.IsNullOrWhiteSpace(toolResultBlock.ToolName))
            {
                toolNode["name"] = toolResultBlock.ToolName;
            }

            return toolNode;
        }

        var role = message.Role switch
        {
            ModelMessageRole.System => "system",
            ModelMessageRole.User => "user",
            ModelMessageRole.Assistant => "assistant",
            ModelMessageRole.Tool => "tool",
            _ => "user"
        };

        var node = new JsonObject
        {
            ["role"] = role
        };

        var textContent = message.TextContent;
        var toolUses = message.Blocks.OfType<ModelToolUseBlock>().ToArray();

        if (!string.IsNullOrWhiteSpace(textContent) || role is "user" or "system")
        {
            node["content"] = textContent;
        }
        else if (toolUses.Length == 0)
        {
            node["content"] = string.Empty;
        }
        // else: Omit 'content' entirely when 'tool_calls' are present and content is empty.

        if (toolUses.Length > 0)
        {
            node["tool_calls"] = new JsonArray(toolUses.Select(toolUse => (JsonNode)new JsonObject
            {
                ["id"] = toolUse.Id,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = toolUse.Name,
                    ["arguments"] = toolUse.ArgumentsJson
                }
            }).ToArray());
        }

        return node;
    }

    private static JsonNode ToCompatibleTool(ModelToolSchema tool)
    {
        JsonNode parameters;
        if (string.IsNullOrWhiteSpace(tool.JsonSchema))
        {
            parameters = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject()
            };
        }
        else
        {
            parameters = JsonNode.Parse(tool.JsonSchema) ?? new JsonObject();
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = parameters
            }
        };
    }
}

internal static class ProviderHttpHelpers
{
    public static HttpRequestMessage CreateRequest(HttpMethod method, ResolvedModelTarget target, string path, JsonObject payload, string? apiKey)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (!string.IsNullOrWhiteSpace(target.Organization))
        {
            request.Headers.Add("OpenAI-Organization", target.Organization);
        }

        if (!string.IsNullOrWhiteSpace(target.Project))
        {
            request.Headers.Add("OpenAI-Project", target.Project);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, string providerName, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new ModelProviderException(
            $"Provider '{providerName}' request failed with status {(int)response.StatusCode}: {body}",
            providerName,
            response.StatusCode,
            isRetryable: response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout);
    }

    public static async IAsyncEnumerable<JsonDocument> ReadSseDocumentsAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataLines = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (dataLines.Count == 0)
                {
                    continue;
                }

                var payload = string.Join("\n", dataLines);
                dataLines.Clear();
                if (payload == "[DONE]")
                {
                    continue;
                }

                yield return JsonDocument.Parse(payload);
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                dataLines.Add(line["data:".Length..].TrimStart());
            }
        }

        if (dataLines.Count > 0)
        {
            var payload = string.Join("\n", dataLines);
            if (payload != "[DONE]")
            {
                yield return JsonDocument.Parse(payload);
            }
        }
    }
}

internal static class CompatibleChatStreamReader
{
    public static async IAsyncEnumerable<ModelResponseChunk> StreamAsync(
        ClawOptions options,
        IProviderHttpClientFactory httpClientFactory,
        ModelRequest request,
        string providerType,
        string defaultPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Target.ProviderType, providerType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelProviderException($"Provider target type '{request.Target.ProviderType}' is not supported by '{providerType}'.", request.Target.ProviderName);
        }

        var providerOptions = options.Providers.Models.FirstOrDefault(model => string.Equals(model.Name, request.Target.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (providerOptions is null)
        {
            throw new ModelProviderException($"Provider configuration '{request.Target.ProviderName}' was not found.", request.Target.ProviderName);
        }

        var client = httpClientFactory.CreateClient(request.Target);
        var payload = OpenAiRequestMapper.CreateCompatibleChatPayload(request);
        var path = string.IsNullOrWhiteSpace(request.Target.RequestPath) ? defaultPath : request.Target.RequestPath!;
        using var httpRequest = ProviderHttpHelpers.CreateRequest(HttpMethod.Post, request.Target, path, payload, providerOptions.ApiKey);
        
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            yield return new ModelResponseChunk(Error: $"Provider '{request.Target.ProviderName}' failed with {(int)response.StatusCode}: {body}");
            yield break;
        }

        var toolCallBuilders = new Dictionary<int, (string? Id, string? Name, StringBuilder Arguments)>();

        await foreach (var document in ProviderHttpHelpers.ReadSseDocumentsAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
        {
            var root = document.RootElement;
            
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                if (root.TryGetProperty("usage", out _))
                {
                    yield return new ModelResponseChunk(Usage: ReadChatUsage(root), StopReason: null);
                }

                continue;
            }

            var choice = choices[0];
            
            if (choice.TryGetProperty("delta", out var delta))
            {
                if (delta.TryGetProperty("content", out var contentElement))
                {
                    var content = contentElement.GetString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        yield return new ModelResponseChunk(TextDelta: content);
                    }
                }

                if (delta.TryGetProperty("tool_calls", out var toolCalls))
                {
                    foreach (var toolCallElement in toolCalls.EnumerateArray())
                    {
                        var index = toolCallElement.TryGetProperty("index", out var indexElement) ? indexElement.GetInt32() : 0;
                        if (!toolCallBuilders.TryGetValue(index, out var state))
                        {
                            state = (null, null, new StringBuilder());
                            toolCallBuilders[index] = state;
                        }

                        if (toolCallElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                        {
                            state.Id = idElement.GetString();
                        }

                        if (toolCallElement.TryGetProperty("function", out var function))
                        {
                            if (function.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                            {
                                state.Name = nameElement.GetString();
                            }

                            if (function.TryGetProperty("arguments", out var argumentsElement) && argumentsElement.ValueKind == JsonValueKind.String)
                            {
                                state.Arguments.Append(argumentsElement.GetString());
                            }
                        }

                        toolCallBuilders[index] = state;
                    }
                }
            }

            if (choice.TryGetProperty("finish_reason", out var finishReasonElement) && finishReasonElement.ValueKind == JsonValueKind.String)
            {
                var finishReason = finishReasonElement.GetString();
                var hasPendingToolCalls = toolCallBuilders.Count > 0;

                if (string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase) || 
                    (string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase) && hasPendingToolCalls))
                {
                    foreach (var state in toolCallBuilders.OrderBy(pair => pair.Key).Select(pair => pair.Value))
                    {
                        yield return new ModelResponseChunk(ToolCall: new ModelToolCall(state.Id ?? Guid.NewGuid().ToString("N"), state.Name ?? string.Empty, state.Arguments.ToString()));
                    }

                    yield return new ModelResponseChunk(Usage: ReadChatUsage(root), StopReason: ModelStopReason.ToolCall);
                }
                else
                {
                    yield return new ModelResponseChunk(
                        Usage: ReadChatUsage(root),
                        StopReason: finishReason switch
                        {
                            "length" => ModelStopReason.Length,
                            "stop" => ModelStopReason.Completed,
                            _ => ModelStopReason.Completed
                        });
                }
            }
        }
    }

    public static ModelUsage? ReadChatUsage(JsonElement element)
    {
        if (!element.TryGetProperty("usage", out var usage))
        {
            return null;
        }

        var input = usage.TryGetProperty("prompt_tokens", out var promptTokens) ? promptTokens.GetInt32() : 0;
        var output = usage.TryGetProperty("completion_tokens", out var completionTokens) ? completionTokens.GetInt32() : 0;
        var total = usage.TryGetProperty("total_tokens", out var totalTokens) ? totalTokens.GetInt32() : input + output;
        return new ModelUsage(input, output, total);
    }
}

/// <summary>
/// 基于 OpenAI Responses API 的 provider 实现。
/// </summary>
public sealed class OpenAiResponsesModelProvider(ClawOptions options, IProviderHttpClientFactory httpClientFactory) : IModelProvider
{
    /// <inheritdoc />
    public ModelProviderMetadata Metadata { get; } = new("openai-responses", "OpenAI Responses", true, true);

    /// <inheritdoc />
    public async IAsyncEnumerable<ModelResponseChunk> StreamAsync(
        ModelRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Target.ProviderType, Metadata.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelProviderException($"Provider target type '{request.Target.ProviderType}' is not supported by '{Metadata.Name}'.", request.Target.ProviderName);
        }

        var providerOptions = options.Providers.Models.FirstOrDefault(model => string.Equals(model.Name, request.Target.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (providerOptions is null)
        {
            throw new ModelProviderException($"Provider configuration '{request.Target.ProviderName}' was not found.", request.Target.ProviderName);
        }

        ValidateRemoteProvider(providerOptions, request.Target.ProviderName);
        var client = httpClientFactory.CreateClient(request.Target);
        var payload = OpenAiRequestMapper.CreateResponsesPayload(request);
        var path = string.IsNullOrWhiteSpace(request.Target.RequestPath) ? "v1/responses" : request.Target.RequestPath!;
        using var httpRequest = ProviderHttpHelpers.CreateRequest(HttpMethod.Post, request.Target, path, payload, providerOptions.ApiKey);
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            yield return new ModelResponseChunk(Error: $"Provider '{request.Target.ProviderName}' failed with {(int)response.StatusCode}: {body}");
            yield break;
        }

        var argumentBuffers = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        var toolNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Responses API 会把文本增量和工具调用参数拆成多种 SSE 事件，这里负责把它们重新组装成统一的 chunk 流。
        await foreach (var document in ProviderHttpHelpers.ReadSseDocumentsAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
        {
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            switch (type)
            {
                case "response.output_text.delta":
                    if (root.TryGetProperty("delta", out var deltaElement))
                    {
                        yield return new ModelResponseChunk(TextDelta: deltaElement.GetString());
                    }
                    break;
                case "response.function_call_arguments.delta":
                    {
                        var callId = root.GetProperty("item_id").GetString() ?? root.GetProperty("call_id").GetString() ?? string.Empty;
                        var delta = root.TryGetProperty("delta", out var argumentsDeltaElement) ? argumentsDeltaElement.GetString() ?? string.Empty : string.Empty;
                        if (!argumentBuffers.TryGetValue(callId, out var builder))
                        {
                            builder = new StringBuilder();
                            argumentBuffers[callId] = builder;
                        }

                        builder.Append(delta);
                        if (root.TryGetProperty("name", out var nameElement) && !string.IsNullOrWhiteSpace(nameElement.GetString()))
                        {
                            toolNames[callId] = nameElement.GetString()!;
                        }
                    }
                    break;
                case "response.output_item.done":
                    {
                        if (!root.TryGetProperty("item", out var item))
                        {
                            break;
                        }

                        if (!string.Equals(item.GetProperty("type").GetString(), "function_call", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        var callId = item.TryGetProperty("call_id", out var callIdElement)
                            ? callIdElement.GetString() ?? string.Empty
                            : item.TryGetProperty("id", out var idElement)
                                ? idElement.GetString() ?? string.Empty
                                : string.Empty;
                        var name = item.TryGetProperty("name", out var nameElement)
                            ? nameElement.GetString() ?? toolNames.GetValueOrDefault(callId, string.Empty)
                            : toolNames.GetValueOrDefault(callId, string.Empty);
                        var arguments = item.TryGetProperty("arguments", out var argumentsElement)
                            ? argumentsElement.GetString() ?? argumentBuffers.GetValueOrDefault(callId)?.ToString() ?? "{}"
                            : argumentBuffers.GetValueOrDefault(callId)?.ToString() ?? "{}";

                        yield return new ModelResponseChunk(ToolCall: new ModelToolCall(callId, name, arguments));
                    }
                    break;
                case "response.completed":
                    yield return new ModelResponseChunk(
                        Usage: TryReadUsage(root.TryGetProperty("response", out var responseElement) ? responseElement : root),
                        StopReason: ModelStopReason.Completed);
                    break;
                case "response.failed":
                case "error":
                    yield return new ModelResponseChunk(
                        Error: root.GetRawText(),
                        StopReason: ModelStopReason.Error);
                    break;
            }
        }
    }

    private static void ValidateRemoteProvider(ModelProviderOptions options, string providerName)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new ModelProviderException($"Provider '{providerName}' is missing BaseUrl.", providerName);
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ModelProviderException($"Provider '{providerName}' is missing ApiKey.", providerName);
        }
    }

    private static ModelUsage? TryReadUsage(JsonElement element)
    {
        if (!element.TryGetProperty("usage", out var usage))
        {
            return null;
        }

        var input = usage.TryGetProperty("input_tokens", out var inputTokens) ? inputTokens.GetInt32() : 0;
        var output = usage.TryGetProperty("output_tokens", out var outputTokens) ? outputTokens.GetInt32() : 0;
        var total = usage.TryGetProperty("total_tokens", out var totalTokens) ? totalTokens.GetInt32() : input + output;
        return new ModelUsage(input, output, total);
    }
}

/// <summary>
/// 兼容 OpenAI Chat Completions 协议的 provider 实现。
/// </summary>
public sealed class OpenAiCompatibleChatModelProvider(ClawOptions options, IProviderHttpClientFactory httpClientFactory) : IModelProvider
{
    /// <inheritdoc />
    public ModelProviderMetadata Metadata { get; } = new("openai-chat-compatible", "OpenAI-compatible Chat", true, true);

    /// <inheritdoc />
    public async IAsyncEnumerable<ModelResponseChunk> StreamAsync(
        ModelRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in CompatibleChatStreamReader.StreamAsync(
                           options,
                           httpClientFactory,
                           request,
                           Metadata.Name,
                           "v1/chat/completions",
                           cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }
}

/// <summary>
/// 面向 Gemini OpenAI-compatible endpoint 的 provider 实现。
/// 复用通用的 chat/completions 映射，但使用 Gemini 默认路径。
/// </summary>
public sealed class GeminiCompatibleChatModelProvider(ClawOptions options, IProviderHttpClientFactory httpClientFactory) : IModelProvider
{
    /// <inheritdoc />
    public ModelProviderMetadata Metadata { get; } = new("gemini-openai-compatible", "Gemini OpenAI-Compatible", true, true);

    /// <inheritdoc />
    public async IAsyncEnumerable<ModelResponseChunk> StreamAsync(
        ModelRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in CompatibleChatStreamReader.StreamAsync(
                           options,
                           httpClientFactory,
                           request,
                           Metadata.Name,
                           "chat/completions",
                           cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }
}