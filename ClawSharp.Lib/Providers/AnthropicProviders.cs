using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Providers;

internal static class AnthropicRequestMapper
{
    private const int DefaultMaxTokens = 4096;

    public static JsonObject CreateMessagesPayload(ModelRequest request)
    {
        var payload = new JsonObject
        {
            ["model"] = request.Target.Model,
            ["stream"] = true,
            ["max_tokens"] = request.MaxTokens ?? DefaultMaxTokens,
            ["messages"] = new JsonArray(request.Messages
                .Where(message => message.Role != ModelMessageRole.System)
                .Select(ToAnthropicMessage)
                .ToArray())
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["system"] = request.SystemPrompt;
        }

        if (request.Temperature is not null)
        {
            payload["temperature"] = request.Temperature.Value;
        }

        if (request.Tools.Count > 0)
        {
            payload["tools"] = new JsonArray(request.Tools.Select(ToAnthropicTool).ToArray());
        }

        if (!string.IsNullOrWhiteSpace(request.ToolChoice))
        {
            payload["tool_choice"] = request.ToolChoice switch
            {
                "none" => new JsonObject { ["type"] = "none" },
                "required" => new JsonObject { ["type"] = "any" },
                _ => new JsonObject { ["type"] = "auto" }
            };
        }

        return payload;
    }

    private static JsonNode ToAnthropicMessage(ModelMessage message)
    {
        if (message.ToolResultBlock is { } toolResult)
        {
            return new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = toolResult.ToolCallId,
                        ["content"] = toolResult.Content
                    }
                }
            };
        }

        var content = new JsonArray();
        foreach (var block in message.Blocks)
        {
            switch (block)
            {
                case ModelTextBlock textBlock:
                    content.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = textBlock.Text
                    });
                    break;
                case ModelToolUseBlock toolUseBlock:
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = toolUseBlock.Id,
                        ["name"] = toolUseBlock.Name,
                        ["input"] = ParseArgumentsObject(toolUseBlock.ArgumentsJson)
                    });
                    break;
            }
        }

        var role = message.Role switch
        {
            ModelMessageRole.Assistant => "assistant",
            _ => "user"
        };

        return new JsonObject
        {
            ["role"] = role,
            ["content"] = content
        };
    }

    private static JsonNode ToAnthropicTool(ModelToolSchema tool)
    {
        JsonNode schema;
        if (string.IsNullOrWhiteSpace(tool.JsonSchema))
        {
            schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject()
            };
        }
        else
        {
            schema = JsonNode.Parse(tool.JsonSchema) ?? new JsonObject();
        }

        return new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["input_schema"] = schema
        };
    }

    private static JsonNode ParseArgumentsObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new JsonObject();
        }

        try
        {
            var parsed = JsonNode.Parse(content);
            return parsed ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}

/// <summary>
/// 基于 Anthropic Messages API 的 provider 实现。
/// 当前版本支持流式文本与 Claude 原生 tool_use/tool_result 回环。
/// </summary>
public sealed class AnthropicMessagesModelProvider(ClawOptions options, IProviderHttpClientFactory httpClientFactory) : IModelProvider
{
    private const string DefaultApiVersion = "2023-06-01";

    /// <inheritdoc />
    public ModelProviderMetadata Metadata { get; } = new("anthropic-messages", "Anthropic Messages", true, true);

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

        if (string.IsNullOrWhiteSpace(providerOptions.BaseUrl))
        {
            throw new ModelProviderException($"Provider '{request.Target.ProviderName}' is missing BaseUrl.", request.Target.ProviderName);
        }

        if (string.IsNullOrWhiteSpace(providerOptions.ApiKey))
        {
            throw new ModelProviderException($"Provider '{request.Target.ProviderName}' is missing ApiKey.", request.Target.ProviderName);
        }

        var client = httpClientFactory.CreateClient(request.Target);
        var payload = AnthropicRequestMapper.CreateMessagesPayload(request);
        var path = string.IsNullOrWhiteSpace(request.Target.RequestPath) ? "v1/messages" : request.Target.RequestPath!;
        using var httpRequest = CreateAnthropicRequest(request.Target, path, payload, providerOptions.ApiKey);
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ProviderHttpHelpers.EnsureSuccessAsync(response, request.Target.ProviderName, cancellationToken).ConfigureAwait(false);

        var toolCallBuilders = new Dictionary<int, (string? Id, string? Name, StringBuilder Arguments)>();

        await foreach (var document in ProviderHttpHelpers.ReadSseDocumentsAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
        {
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            switch (type)
            {
                case "content_block_start":
                    if (root.TryGetProperty("index", out var startIndexElement) &&
                        root.TryGetProperty("content_block", out var contentBlock) &&
                        contentBlock.TryGetProperty("type", out var contentBlockType) &&
                        string.Equals(contentBlockType.GetString(), "tool_use", StringComparison.OrdinalIgnoreCase))
                    {
                        var index = startIndexElement.GetInt32();
                        var id = contentBlock.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                        var name = contentBlock.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        var arguments = new StringBuilder();
                        if (contentBlock.TryGetProperty("input", out var inputElement) &&
                            HasMeaningfulInput(inputElement))
                        {
                            arguments.Append(inputElement.GetRawText());
                        }

                        toolCallBuilders[index] = (id, name, arguments);
                    }
                    break;
                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var deltaElement) &&
                        deltaElement.TryGetProperty("type", out var deltaTypeElement) &&
                        string.Equals(deltaTypeElement.GetString(), "text_delta", StringComparison.OrdinalIgnoreCase) &&
                        deltaElement.TryGetProperty("text", out var textElement))
                    {
                        yield return new ModelResponseChunk(TextDelta: textElement.GetString());
                    }
                    else if (root.TryGetProperty("index", out var deltaIndexElement) &&
                             deltaElement.TryGetProperty("type", out deltaTypeElement) &&
                             string.Equals(deltaTypeElement.GetString(), "input_json_delta", StringComparison.OrdinalIgnoreCase) &&
                             deltaElement.TryGetProperty("partial_json", out var partialJsonElement))
                    {
                        var index = deltaIndexElement.GetInt32();
                        if (!toolCallBuilders.TryGetValue(index, out var toolState))
                        {
                            toolState = (null, null, new StringBuilder());
                        }

                        toolState.Arguments.Append(partialJsonElement.GetString());
                        toolCallBuilders[index] = toolState;
                    }
                    break;
                case "content_block_stop":
                    if (root.TryGetProperty("index", out var stopIndexElement) &&
                        toolCallBuilders.TryGetValue(stopIndexElement.GetInt32(), out var state) &&
                        !string.IsNullOrWhiteSpace(state.Name))
                    {
                        var arguments = state.Arguments.Length == 0 ? "{}" : state.Arguments.ToString();
                        yield return new ModelResponseChunk(
                            ToolCall: new ModelToolCall(state.Id ?? Guid.NewGuid().ToString("N"), state.Name!, arguments));
                        toolCallBuilders.Remove(stopIndexElement.GetInt32());
                    }
                    break;
                case "message_delta":
                    yield return new ModelResponseChunk(
                        Usage: TryReadUsage(root),
                        StopReason: TryReadStopReason(root));
                    break;
                case "message_stop":
                    yield return new ModelResponseChunk(StopReason: ModelStopReason.Completed);
                    break;
                case "error":
                    yield return new ModelResponseChunk(
                        Error: root.GetRawText(),
                        StopReason: ModelStopReason.Error);
                    break;
            }
        }
    }

    private static HttpRequestMessage CreateAnthropicRequest(ResolvedModelTarget target, string path, JsonObject payload, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", DefaultApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static ModelUsage? TryReadUsage(JsonElement element)
    {
        if (!element.TryGetProperty("usage", out var usage))
        {
            return null;
        }

        var input = usage.TryGetProperty("input_tokens", out var inputTokens) ? inputTokens.GetInt32() : 0;
        var output = usage.TryGetProperty("output_tokens", out var outputTokens) ? outputTokens.GetInt32() : 0;
        var total = input + output;
        return new ModelUsage(input, output, total);
    }

    private static ModelStopReason? TryReadStopReason(JsonElement element)
    {
        if (!element.TryGetProperty("delta", out var delta) ||
            !delta.TryGetProperty("stop_reason", out var stopReasonElement) ||
            stopReasonElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return stopReasonElement.GetString() switch
        {
            "max_tokens" => ModelStopReason.Length,
            "tool_use" => ModelStopReason.ToolCall,
            "end_turn" => ModelStopReason.Completed,
            _ => ModelStopReason.Completed
        };
    }

    private static bool HasMeaningfulInput(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Any(),
            JsonValueKind.Array => element.GetArrayLength() > 0,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Number => true,
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            _ => false
        };
    }
}
