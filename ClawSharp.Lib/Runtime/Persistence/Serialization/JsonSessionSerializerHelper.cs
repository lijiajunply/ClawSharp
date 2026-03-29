using ClawSharp.Lib.Providers;

namespace ClawSharp.Lib.Runtime;

internal static class JsonSessionSerializerHelper
{
    public static System.Text.Json.JsonElement ParseElement(string json) =>
        System.Text.Json.JsonSerializer.SerializeToElement(System.Text.Json.JsonDocument.Parse(json).RootElement);

    public static string SerializeBlocks(IReadOnlyList<ModelContentBlock> blocks) =>
        System.Text.Json.JsonSerializer.Serialize(blocks.Select(ToPersistedBlock).ToArray());

    public static IReadOnlyList<ModelContentBlock> ParseBlocks(string? json, PromptMessageRole role, string content, string? name, string? toolCallId)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var persisted = System.Text.Json.JsonSerializer.Deserialize<PersistedContentBlock[]>(json);
                if (persisted is { Length: > 0 })
                {
                    return persisted.Select(ToBlock).ToArray();
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        return PromptMessageFallbackBlocks(role, content, name, toolCallId);
    }

    private static IReadOnlyList<ModelContentBlock> PromptMessageFallbackBlocks(PromptMessageRole role, string content, string? name, string? toolCallId)
    {
        if (role == PromptMessageRole.Tool && !string.IsNullOrWhiteSpace(toolCallId))
        {
            return [new ModelToolResultBlock(toolCallId, content, name)];
        }

        if (role == PromptMessageRole.Assistant &&
            !string.IsNullOrWhiteSpace(toolCallId) &&
            !string.IsNullOrWhiteSpace(name))
        {
            return [new ModelToolUseBlock(toolCallId, name, content)];
        }

        return [new ModelTextBlock(content)];
    }

    private static PersistedContentBlock ToPersistedBlock(ModelContentBlock block) => block switch
    {
        ModelTextBlock text => new PersistedContentBlock("text", Text: text.Text),
        ModelToolUseBlock toolUse => new PersistedContentBlock("tool_use", Id: toolUse.Id, Name: toolUse.Name, ArgumentsJson: toolUse.ArgumentsJson),
        ModelToolResultBlock toolResult => new PersistedContentBlock("tool_result", ToolCallId: toolResult.ToolCallId, Content: toolResult.Content, ToolName: toolResult.ToolName),
        _ => throw new NotSupportedException($"Unsupported content block type '{block.GetType().Name}'.")
    };

    private static ModelContentBlock ToBlock(PersistedContentBlock block) => block.Type switch
    {
        "text" => new ModelTextBlock(block.Text ?? string.Empty),
        "tool_use" => new ModelToolUseBlock(block.Id ?? string.Empty, block.Name ?? string.Empty, block.ArgumentsJson ?? "{}"),
        "tool_result" => new ModelToolResultBlock(block.ToolCallId ?? string.Empty, block.Content ?? string.Empty, block.ToolName),
        _ => new ModelTextBlock(block.Text ?? block.Content ?? string.Empty)
    };

    private sealed record PersistedContentBlock(
        string Type,
        string? Text = null,
        string? Id = null,
        string? Name = null,
        string? ArgumentsJson = null,
        string? ToolCallId = null,
        string? Content = null,
        string? ToolName = null);
}
