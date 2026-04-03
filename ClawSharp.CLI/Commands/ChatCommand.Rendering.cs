using System.Text.Json;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ClawSharp.CLI.Commands;

public static partial class ChatCommand
{
    private static async Task RunTurnAsync(ReplState state, string input)
    {
        var processedInput = await InputPreprocessor.ProcessAsync(input, state.PromptHandler.CurrentDirectory);
        await state.Runtime.AppendUserMessageAsync(state.SessionId, processedInput);

        var hasTextOutput = false;
        string? finalAssistantMessage = null;
        PerformanceMetrics? performance = null;
        var streamingMarkdown = new Markdown(string.Empty);
        var toolTimeline = new ToolTimeline();
        var streamingRenderable = new StreamingAssistantRenderable(streamingMarkdown, toolTimeline);

        try
        {
            await AnsiConsole.Live(streamingRenderable)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Visible)
                .StartAsync(async ctx =>
                {
                    await foreach (var @event in state.Runtime.RunTurnStreamingAsync(state.SessionId))
                    {
                        if (@event.Delta is not null)
                        {
                            hasTextOutput = true;
                            finalAssistantMessage = string.Concat(finalAssistantMessage, @event.Delta);
                            streamingRenderable.Update(finalAssistantMessage);
                            ctx.UpdateTarget(streamingRenderable);
                        }

                        if (!string.IsNullOrWhiteSpace(@event.EventType) && @event.EventPayload is { } payload)
                        {
                            if (HandleStreamingEvent(toolTimeline, @event.EventType!, payload))
                            {
                                ctx.UpdateTarget(streamingRenderable);
                            }
                        }

                        if (@event.FinalResult is not null)
                        {
                            finalAssistantMessage = @event.FinalResult.AssistantMessage;
                            performance = @event.FinalResult.Performance;
                            streamingRenderable.Update(finalAssistantMessage);
                            ctx.UpdateTarget(streamingRenderable);
                        }
                    }
                })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            CliErrorHandler.Handle(ex);
            hasTextOutput = true;
        }

        if (!hasTextOutput)
        {
            AnsiConsole.Write(new StreamingAssistantRenderable(new Markdown(I18n.T("Chat.NoTextResponse")), toolTimeline));
        }

        if (performance is not null)
        {
            var cacheStatus = performance.AgentLaunchPlanCacheHit ? "[green]hit[/]" : "[yellow]miss[/]";
            var mcpStatus = performance.TotalMcpConnections == 0
                ? "[grey]n/a[/]"
                : performance.McpHandshakeAvoided
                    ? $"[green]{performance.ReusedMcpConnections}/{performance.TotalMcpConnections} reused[/]"
                    : "[yellow]cold[/]";
            AnsiConsole.MarkupLine(I18n.T("Chat.TurnSummary", cacheStatus, mcpStatus));
        }

        state.LastToolTimeline = toolTimeline;

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private static Task<CommandDispatchResult> HandleToolTraceAsync(ReplState state, string arguments)
    {
        if (state.LastToolTimeline is null || state.LastToolTimeline.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]当前会话里还没有可展开的工具调用详情。先运行一轮包含工具调用的对话，再使用 /tooltrace。[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        var snapshot = state.LastToolTimeline.CreateSnapshot();
        if (string.IsNullOrWhiteSpace(arguments))
        {
            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn(new TableColumn("[grey]#[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Kind[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Name[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Status[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Time[/]").NoWrap());
            table.AddColumn("[grey]Summary[/]");

            for (var i = 0; i < snapshot.Count; i++)
            {
                var item = snapshot[i];
                table.AddRow(
                    (i + 1).ToString(),
                    item.KindBadge,
                    item.NameMarkup,
                    item.StatusMarkup,
                    item.Elapsed.EscapeMarkup(),
                    (item.Summary ?? "[grey]No summary[/]").EscapeMarkup());
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[grey]用 /tooltrace <编号> 查看完整参数与结果，例如 /tooltrace 1[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        ToolTraceSnapshot? selected = null;
        if (arguments.Trim().Equals("last", StringComparison.OrdinalIgnoreCase))
        {
            selected = snapshot.LastOrDefault();
        }
        else if (int.TryParse(arguments.Trim(), out var index) && index >= 1 && index <= snapshot.Count)
        {
            selected = snapshot[index - 1];
        }

        if (selected is null)
        {
            AnsiConsole.MarkupLine($"[yellow]未找到工具调用详情：{arguments.EscapeMarkup()}[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        var detailRows = new List<IRenderable>
        {
            new Markup($"{selected.KindBadge} {selected.NameMarkup} {selected.StatusMarkup}"),
            new Markup($"[grey]耗时:[/] {selected.Elapsed.EscapeMarkup()}")
        };

        if (!string.IsNullOrWhiteSpace(selected.Summary))
        {
            detailRows.Add(new Markup($"[grey]摘要:[/] {selected.Summary!.EscapeMarkup()}"));
        }

        detailRows.Add(new Rule("[grey]Arguments[/]"));
        detailRows.Add(new Text(selected.ArgumentsRaw ?? "{}"));
        detailRows.Add(new Rule("[grey]Result[/]"));
        detailRows.Add(new Text(selected.ResultRaw ?? "(no result)"));

        AnsiConsole.Write(new Panel(new Rows(detailRows.ToArray()))
        {
            Header = new PanelHeader($"Tool Trace #{selected.Index}"),
            Border = BoxBorder.Rounded
        });

        return Task.FromResult(CommandDispatchResult.Handled());
    }

    private static bool HandleStreamingEvent(ToolTimeline toolTimeline, string eventType, JsonElement payload)
    {
        switch (eventType)
        {
            case "worker.tool.requested":
                var requestedToolName = payload.TryGetProperty("name", out var requestedNameValue)
                    ? requestedNameValue.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(requestedToolName))
                {
                    return false;
                }

                var requestedToolId = payload.TryGetProperty("id", out var requestedIdValue)
                    ? requestedIdValue.GetString()
                    : null;
                var requestedArguments = payload.TryGetProperty("argumentsJson", out var requestedArgumentsValue)
                    ? requestedArgumentsValue.GetString()
                    : null;
                var requestedIsAgent = payload.TryGetProperty("isAgent", out var requestedIsAgentValue) &&
                                       requestedIsAgentValue.ValueKind is JsonValueKind.True;
                toolTimeline.MarkRequested(requestedToolId, requestedToolName, requestedArguments, requestedIsAgent);
                return true;

            case "worker.tool.completed":
                var completedToolId = payload.TryGetProperty("toolCallId", out var completedIdValue)
                    ? completedIdValue.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(completedToolId))
                {
                    return false;
                }

                var completedToolName = payload.TryGetProperty("toolName", out var completedNameValue)
                    ? completedNameValue.GetString()
                    : null;
                var completedStatus = payload.TryGetProperty("status", out var completedStatusValue)
                    ? completedStatusValue.GetString()
                    : null;
                JsonElement? completedPayload = payload.TryGetProperty("payload", out var completedPayloadValue)
                    ? completedPayloadValue
                    : null;
                var completedIsAgent = payload.TryGetProperty("isAgent", out var completedIsAgentValue) &&
                                       completedIsAgentValue.ValueKind is JsonValueKind.True;
                toolTimeline.MarkCompleted(completedToolId, completedToolName, completedStatus, completedPayload, completedIsAgent);
                return true;

            default:
                return false;
        }
    }

    private static void ShowWelcomeHeader(string agentId, string threadSpaceName)
    {
        var grid = new Grid().AddColumn();
        grid.AddRow(new Text(I18n.T("Chat.WelcomeHeader"), new Style(Color.Blue, decoration: Decoration.Bold)));
        grid.AddRow(new Markup($"[grey]{I18n.T("Chat.AgentLabel")}[/] [green]{agentId.EscapeMarkup()}[/]   [grey]{I18n.T("Chat.SpaceLabel")}[/] [blue]{threadSpaceName.EscapeMarkup()}[/]"));
        grid.AddRow(new Text(I18n.T("Chat.TypeHelp"), new Style(Color.Grey)));

        var panel = new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
        AnsiConsole.Write(panel);
    }

    private static string GetPrompt(ReplState state)
    {
        var space = state.CurrentThreadSpace;
        var agentId = state.AgentId;
        var mode = state.Session.Record.Mode;

        var spaceName = space.Name;
        if (spaceName.Length > 20)
        {
            spaceName = spaceName[..17] + "...";
        }

        var displayAgentId = agentId;
        if (displayAgentId.Length > 10)
        {
            displayAgentId = $"{displayAgentId[0]}{displayAgentId.Length - 2}{displayAgentId[^1]}";
        }

        var color = space.IsGlobal ? "bold blue" : "cyan";
        var prompt = $"[{color}]{spaceName.EscapeMarkup()}[/][grey][[{displayAgentId.EscapeMarkup()}]][/]";

        if (mode == SessionMode.Plan)
        {
            prompt += " [bold yellow][PLAN][/]";
        }

        return prompt + " > ";
    }

    private static string ShortId(SessionId sessionId) =>
        sessionId.Value.Length <= 8 ? sessionId.Value : sessionId.Value[..8];

    private sealed class StreamingAssistantRenderable(Markdown markdown, ToolTimeline toolTimeline) : IRenderable
    {
        public void Update(string? content)
        {
            markdown.Update(content ?? string.Empty);
        }

        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            var renderable = CreateBodyRenderable();
            return renderable.Measure(options, maxWidth);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var renderable = CreateBodyRenderable();
            return renderable.Render(options, maxWidth);
        }

        private IRenderable CreateBodyRenderable()
        {
            var rows = new List<IRenderable>
            {
                new Markup(I18n.T("Chat.AgentPromptHeader"))
            };

            var toolSummary = toolTimeline.CreateRenderable();
            if (toolSummary is not null)
            {
                rows.Add(toolSummary);
            }

            if (!string.IsNullOrWhiteSpace(markdown.Content))
            {
                rows.Add(markdown.HasRichContent
                    ? markdown
                    : new Text(markdown.Content));
            }

            return new Rows(rows.ToArray());
        }
    }

    private sealed class ToolTimeline
    {
        private readonly List<ToolCallViewModel> _items = [];
        private readonly Dictionary<string, ToolCallViewModel> _byId = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _items.Count;

        public void MarkRequested(string? toolCallId, string toolName, string? argumentsJson, bool isAgent)
        {
            var item = FindOrCreate(toolCallId, toolName);
            item.ToolName = toolName;
            item.IsAgent = isAgent;
            item.StartedAtUtc = DateTimeOffset.UtcNow;
            item.CompletedAtUtc = null;
            item.ArgumentsSummary = SummarizeToolRequest(toolName, argumentsJson, isAgent);
            item.ArgumentsRaw = PrettyPrintJson(argumentsJson);
            item.Status = item.IsAgent ? "Delegating" : "Running";
            item.IsCompleted = false;
            item.ResultSummary = null;
            item.ResultRaw = null;
            item.StatusStyle = "yellow";
        }

        public void MarkCompleted(string toolCallId, string? toolName, string? status, JsonElement? payload, bool isAgent)
        {
            var resolvedName = string.IsNullOrWhiteSpace(toolName) ? toolCallId : toolName;
            var item = FindOrCreate(toolCallId, resolvedName!);
            if (!string.IsNullOrWhiteSpace(toolName))
            {
                item.ToolName = toolName!;
            }

            item.IsAgent = isAgent;
            item.IsCompleted = true;
            item.CompletedAtUtc = DateTimeOffset.UtcNow;
            item.Status = status switch
            {
                "Success" => "Done",
                "Denied" => "Denied",
                "ApprovalRequired" => "Needs Approval",
                "Failed" => "Failed",
                _ => status ?? "Done"
            };
            item.StatusStyle = status switch
            {
                "Success" => "green",
                "Denied" => "red",
                "Failed" => "red",
                "ApprovalRequired" => "yellow",
                _ => "grey"
            };
            item.ResultSummary = payload is { } value
                ? SummarizeToolResult(item.ToolName, value, isAgent)
                : null;
            item.ResultRaw = payload is { } rawPayload
                ? PrettyPrintJson(rawPayload.GetRawText())
                : null;
        }

        public IRenderable? CreateRenderable()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Expand();
            table.AddColumn(new TableColumn("[grey]Kind[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Name[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Status[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Time[/]").NoWrap());
            table.AddColumn("[grey]Details[/]");

            foreach (var item in _items.TakeLast(6))
            {
                var detail = item.IsCompleted
                    ? item.ResultSummary ?? "[grey]No result payload[/]"
                    : item.ArgumentsSummary ?? "[grey]No arguments[/]";
                table.AddRow(
                    item.GetKindBadge(),
                    item.GetNameMarkup(),
                    $"[{item.StatusStyle}]{item.Status.EscapeMarkup()}[/]",
                    item.GetElapsedLabel().EscapeMarkup(),
                    detail);
            }

            return new Rows(
                new Markup("[grey]Tool Activity[/]"),
                table);
        }

        public IReadOnlyList<ToolTraceSnapshot> CreateSnapshot()
        {
            return _items.Select((item, index) => new ToolTraceSnapshot(
                index + 1,
                item.GetKindBadge(),
                item.GetNameMarkup(),
                $"[{item.StatusStyle}]{item.Status.EscapeMarkup()}[/]",
                item.GetElapsedLabel(),
                item.IsCompleted ? item.ResultSummary ?? item.ArgumentsSummary : item.ArgumentsSummary,
                item.ArgumentsRaw,
                item.ResultRaw)).ToArray();
        }

        private ToolCallViewModel FindOrCreate(string? toolCallId, string toolName)
        {
            if (!string.IsNullOrWhiteSpace(toolCallId) && _byId.TryGetValue(toolCallId, out var existing))
            {
                return existing;
            }

            var created = new ToolCallViewModel
            {
                ToolCallId = toolCallId,
                ToolName = toolName,
                IsAgent = false,
                Status = "Queued",
                StatusStyle = "grey"
            };
            _items.Add(created);
            if (!string.IsNullOrWhiteSpace(toolCallId))
            {
                _byId[toolCallId] = created;
            }

            return created;
        }
    }

    private sealed class ToolCallViewModel
    {
        public string? ToolCallId { get; init; }
        public required string ToolName { get; set; }
        public required string Status { get; set; }
        public required string StatusStyle { get; set; }
        public bool IsAgent { get; set; }
        public bool IsCompleted { get; set; }
        public string? ArgumentsSummary { get; set; }
        public string? ResultSummary { get; set; }
        public string? ArgumentsRaw { get; set; }
        public string? ResultRaw { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }

        public string GetElapsedLabel()
        {
            if (StartedAtUtc is null)
            {
                return "-";
            }

            var end = CompletedAtUtc ?? DateTimeOffset.UtcNow;
            var elapsed = end - StartedAtUtc.Value;
            if (elapsed.TotalSeconds < 1)
            {
                return $"{Math.Max(elapsed.TotalMilliseconds, 1):0} ms";
            }

            if (elapsed.TotalMinutes < 1)
            {
                return $"{elapsed.TotalSeconds:0.0} s";
            }

            return $"{elapsed.TotalMinutes:0.0} min";
        }

        public string GetKindBadge()
        {
            var (label, color) = GetToolKind();
            return $"[{color}][[{label}]][/]";
        }

        public string GetNameMarkup()
        {
            var (_, color) = GetToolKind();
            return $"[{color}]{ToolName.EscapeMarkup()}[/]";
        }

        private (string Label, string Color) GetToolKind()
        {
            if (IsAgent)
            {
                return ("AGENT", "deeppink2");
            }

            return ToolName switch
            {
                "shell_run" => ("CMD", "orange1"),
                "file_read" or "file_list" or "file_tree" or "search_text" or "search_files" or "csv_read" or "pdf_read"
                    => ("FS-R", "deepskyblue1"),
                "file_write" => ("FS-W", "yellow1"),
                "web_search" or "web_browser" => ("NET", "springgreen3"),
                "git_ops" => ("GIT", "mediumpurple"),
                "system_info" or "system_processes" => ("SYS", "grey70"),
                "email_send" => ("MAIL", "turquoise2"),
                _ => ("TOOL", "grey70")
            };
        }
    }

    private sealed record ToolTraceSnapshot(
        int Index,
        string KindBadge,
        string NameMarkup,
        string StatusMarkup,
        string Elapsed,
        string? Summary,
        string? ArgumentsRaw,
        string? ResultRaw);

    private static string? SummarizeToolRequest(string toolName, string? argumentsJson, bool isAgent)
    {
        if (isAgent)
        {
            var delegatedQuery = TryReadJsonString(argumentsJson, "query");
            return delegatedQuery is null
                ? SummarizeJson(argumentsJson)
                : $"query: {TruncateForDisplay(delegatedQuery)}";
        }

        return toolName switch
        {
            "shell_run" => TryReadJsonString(argumentsJson, "command") is { } command
                ? $"command: {TruncateForDisplay(command)}"
                : SummarizeJson(argumentsJson),
            "file_read" or "file_write" or "file_list" or "file_tree" => TryReadJsonString(argumentsJson, "path") is { } path
                ? $"path: {TruncateForDisplay(path)}"
                : SummarizeJson(argumentsJson),
            "search_text" => BuildSearchTextSummary(argumentsJson),
            "search_files" => BuildSearchFilesSummary(argumentsJson),
            _ => SummarizeJson(argumentsJson)
        };
    }

    private static string? SummarizeToolResult(string toolName, JsonElement payload, bool isAgent)
    {
        if (isAgent)
        {
            if (payload.TryGetProperty("result", out var result))
            {
                return SummarizeJson(result);
            }

            return SummarizeJson(payload);
        }

        return toolName switch
        {
            "shell_run" => BuildShellResultSummary(payload),
            "file_read" => payload.TryGetProperty("path", out var readPath)
                ? $"read: {TruncateForDisplay(readPath.GetString() ?? string.Empty)}"
                : SummarizeJson(payload),
            "file_write" => BuildFileWriteResultSummary(payload),
            "file_list" => BuildFileListResultSummary(payload),
            "file_tree" => payload.TryGetProperty("path", out var treePath)
                ? $"tree: {TruncateForDisplay(treePath.GetString() ?? string.Empty)}"
                : SummarizeJson(payload),
            "search_text" or "search_files" => payload.ValueKind == JsonValueKind.Array
                ? $"{payload.GetArrayLength()} match(es)"
                : SummarizeJson(payload),
            _ => SummarizeJson(payload)
        };
    }

    private static string? SummarizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return SummarizeJson(document.RootElement);
        }
        catch (JsonException)
        {
            return TruncateForDisplay(json);
        }
    }

    private static string SummarizeJson(JsonElement element)
    {
        var summary = element.ValueKind switch
        {
            JsonValueKind.Object => SummarizeObject(element),
            JsonValueKind.Array => $"{element.GetArrayLength()} item(s)",
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };

        return TruncateForDisplay(summary);
    }

    private static string SummarizeObject(JsonElement element)
    {
        var parts = new List<string>();
        foreach (var property in element.EnumerateObject().Take(4))
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Array => $"{property.Value.GetArrayLength()} item(s)",
                JsonValueKind.Object => "{...}",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => property.Value.GetRawText()
            };
            parts.Add($"{property.Name}: {value}");
        }

        if (parts.Count == 0)
        {
            return "{}";
        }

        var suffix = element.EnumerateObject().Skip(4).Any() ? ", ..." : string.Empty;
        return string.Join(", ", parts) + suffix;
    }

    private static string TruncateForDisplay(string value)
    {
        var singleLine = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return singleLine.Length > 96
            ? singleLine[..93] + "..."
            : singleLine;
    }

    private static string PrettyPrintJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string? TryReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildSearchTextSummary(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            var query = root.TryGetProperty("query", out var queryValue) ? queryValue.GetString() : null;
            var path = root.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
            if (query is null && path is null)
            {
                return SummarizeJson(argumentsJson);
            }

            return $"query: {TruncateForDisplay(query ?? string.Empty)}" +
                   (string.IsNullOrWhiteSpace(path) ? string.Empty : $", path: {TruncateForDisplay(path)}");
        }
        catch (JsonException)
        {
            return SummarizeJson(argumentsJson);
        }
    }

    private static string? BuildSearchFilesSummary(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            var pattern = root.TryGetProperty("pattern", out var patternValue) ? patternValue.GetString() : null;
            var path = root.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
            if (pattern is null && path is null)
            {
                return SummarizeJson(argumentsJson);
            }

            return $"pattern: {TruncateForDisplay(pattern ?? string.Empty)}" +
                   (string.IsNullOrWhiteSpace(path) ? string.Empty : $", path: {TruncateForDisplay(path)}");
        }
        catch (JsonException)
        {
            return SummarizeJson(argumentsJson);
        }
    }

    private static string BuildShellResultSummary(JsonElement payload)
    {
        var parts = new List<string>();
        if (payload.TryGetProperty("exitCode", out var exitCode))
        {
            parts.Add($"exit: {exitCode.GetRawText()}");
        }

        if (payload.TryGetProperty("stdout", out var stdout))
        {
            var stdoutText = stdout.GetString();
            if (!string.IsNullOrWhiteSpace(stdoutText))
            {
                parts.Add($"stdout: {TruncateForDisplay(stdoutText)}");
            }
        }

        if (payload.TryGetProperty("stderr", out var stderr))
        {
            var stderrText = stderr.GetString();
            if (!string.IsNullOrWhiteSpace(stderrText))
            {
                parts.Add($"stderr: {TruncateForDisplay(stderrText)}");
            }
        }

        return parts.Count == 0 ? SummarizeJson(payload) : string.Join(", ", parts);
    }

    private static string BuildFileWriteResultSummary(JsonElement payload)
    {
        var parts = new List<string>();
        if (payload.TryGetProperty("path", out var path))
        {
            parts.Add($"wrote: {TruncateForDisplay(path.GetString() ?? string.Empty)}");
        }

        if (payload.TryGetProperty("bytes", out var bytes))
        {
            parts.Add($"bytes: {bytes.GetRawText()}");
        }

        return parts.Count == 0 ? SummarizeJson(payload) : string.Join(", ", parts);
    }

    private static string BuildFileListResultSummary(JsonElement payload)
    {
        var parts = new List<string>();
        if (payload.TryGetProperty("path", out var path))
        {
            parts.Add($"path: {TruncateForDisplay(path.GetString() ?? string.Empty)}");
        }

        if (payload.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            parts.Add($"entries: {entries.GetArrayLength()}");
        }

        return parts.Count == 0 ? SummarizeJson(payload) : string.Join(", ", parts);
    }
}
