using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text.RegularExpressions;

namespace ClawSharp.CLI.Infrastructure;

/// <summary>
/// A simple Markdown renderer for Spectre.Console that handles common formatting.
/// Supports streaming updates by being re-rendered.
/// </summary>
public sealed class Markdown : IRenderable
{
    private string _content;
    private MarkdownStreamingState _state;

    public Markdown(string content)
    {
        _content = content ?? string.Empty;
        _state = MarkdownStreamingState.Analyze(_content);
    }

    /// <summary>
    /// Updates the content for streaming scenarios.
    /// </summary>
    public void Update(string content)
    {
        _content = content ?? string.Empty;
        _state = MarkdownStreamingState.Analyze(_content);
    }

    /// <summary>
    /// 当前内容是否包含值得启用富渲染的 Markdown 结构。
    /// </summary>
    public bool HasRichContent => _state.HasRichContent;

    /// <summary>
    /// 当前原始内容。
    /// </summary>
    public string Content => _content;

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(maxWidth, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var result = new List<Segment>();
        var lines = _content.Split('\n');
        bool inTable = false;
        var tableRows = new List<string[]>();
        var codeBlock = new List<string>();
        string? codeLanguage = null;
        var inCodeBlock = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmedLine = line.Trim();

            // Code Blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inTable) { RenderTable(result, tableRows, options, maxWidth); inTable = false; }
                if (inCodeBlock)
                {
                    RenderCodeBlock(result, codeBlock, codeLanguage, options, maxWidth, closed: true);
                    codeBlock.Clear();
                    codeLanguage = null;
                    inCodeBlock = false;
                }
                else
                {
                    codeLanguage = trimmedLine[3..].Trim();
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlock.Add(line);
                continue;
            }

            // Horizontal Rule (3 or more -, *, or _)
            if (Regex.IsMatch(trimmedLine, @"^([-*_])\1{2,}$"))
            {
                if (inTable) { RenderTable(result, tableRows, options, maxWidth); inTable = false; }
                result.AddRange(((IRenderable)new Rule()).Render(options, maxWidth));
                continue;
            }

            // Tables (Basic support)
            bool isTableLine = trimmedLine.Contains("|") && (trimmedLine.StartsWith("|") || trimmedLine.EndsWith("|"));
            if (isTableLine)
            {
                inTable = true;
                var cells = trimmedLine.Split('|', StringSplitOptions.TrimEntries)
                    .Where((c, i) => !(i == 0 && string.IsNullOrEmpty(c)) && !(i == trimmedLine.Split('|').Length - 1 && string.IsNullOrEmpty(c)))
                    .ToArray();

                // Skip separator rows like |---|---|
                if (cells.Length > 0 && cells.All(c => c.All(ch => ch == '-' || ch == ':' || ch == ' ')))
                {
                    // Separator row, skip but stay in table mode
                }
                else
                {
                    tableRows.Add(cells);
                }
                continue;
            }
            else if (inTable)
            {
                RenderTable(result, tableRows, options, maxWidth);
                inTable = false;
                // Continue processing this line as it might contain other content
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                result.Add(new Segment(Environment.NewLine));
                continue;
            }

            string markupText;

            // Headings
            if (trimmedLine.StartsWith("# "))
            {
                markupText = $"[bold yellow underline]{trimmedLine[2..].Trim().EscapeMarkup()}[/]";
            }
            else if (trimmedLine.StartsWith("## "))
            {
                markupText = $"[bold green underline]{trimmedLine[3..].Trim().EscapeMarkup()}[/]";
            }
            else if (trimmedLine.StartsWith("### "))
            {
                markupText = $"[bold blue underline]{trimmedLine[4..].Trim().EscapeMarkup()}[/]";
            }
            // List items
            else if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ") || Regex.IsMatch(trimmedLine, @"^\d+\. "))
            {
                var match = Regex.Match(line, @"^(\s*)([-*]|\d+\.)\s+(.*)");
                if (match.Success)
                {
                    var indent = match.Groups[1].Value;
                    var bullet = match.Groups[2].Value;
                    var content = match.Groups[3].Value;
                    markupText = indent + $"[blue]{(bullet.EndsWith(".") ? bullet : "•")}[/] " + ProcessInline(content);
                }
                else
                {
                    markupText = ProcessInline(line);
                }
            }
            else
            {
                markupText = ProcessInline(line);
            }

            result.AddRange(((IRenderable)new Markup(markupText)).Render(options, maxWidth));
            result.Add(new Segment(Environment.NewLine));
        }

        if (inTable)
        {
            RenderTable(result, tableRows, options, maxWidth);
        }

        if (inCodeBlock)
        {
            RenderCodeBlock(result, codeBlock, codeLanguage, options, maxWidth, closed: false);
        }

        return result;
    }

    private void RenderTable(List<Segment> result, List<string[]> rows, RenderOptions options, int maxWidth)
    {
        if (rows.Count == 0) return;

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        
        var colCount = rows.Max(r => r.Length);
        for (int i = 0; i < colCount; i++) table.AddColumn(new TableColumn(""));
        
        foreach (var row in rows)
        {
            var paddedRow = new string[colCount];
            for (int i = 0; i < colCount; i++)
            {
                paddedRow[i] = i < row.Length ? ProcessInline(row[i]) : "";
            }
            table.AddRow(paddedRow);
        }

        result.AddRange(((IRenderable)table).Render(options, maxWidth));
        result.Add(new Segment(Environment.NewLine));
        rows.Clear();
    }

    private static void RenderCodeBlock(
        List<Segment> result,
        IReadOnlyList<string> codeLines,
        string? language,
        RenderOptions options,
        int maxWidth,
        bool closed)
    {
        var code = string.Join(Environment.NewLine, codeLines);
        IRenderable renderable;

        var mappedLanguage = MapLanguage(language);
        if (closed && !string.IsNullOrWhiteSpace(mappedLanguage))
        {
            renderable = CreateHighlightedCodeBlock(code, mappedLanguage, language, closed);
        }
        else
        {
            renderable = CreatePlainCodeBlock(code, language, closed);
        }

        result.AddRange(renderable.Render(options, maxWidth));
        result.Add(new Segment(Environment.NewLine));
    }

    private static IRenderable CreatePlainCodeBlock(string code, string? language, bool closed)
    {
        var title = string.IsNullOrWhiteSpace(language)
            ? "code"
            : closed
                ? language.Trim()
                : $"{language.Trim()} (streaming)";

        return new Panel(new Text(code, new Style(Color.Grey)))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };
    }

    private static IRenderable CreateHighlightedCodeBlock(string code, string mappedLanguage, string? originalLanguage, bool closed)
    {
        var highlightedLines = code.Split(Environment.NewLine)
            .Select(line => new Markup(HighlightCodeLine(line, mappedLanguage)))
            .Cast<IRenderable>()
            .ToArray();

        var title = string.IsNullOrWhiteSpace(originalLanguage)
            ? mappedLanguage
            : closed
                ? originalLanguage.Trim()
                : $"{originalLanguage.Trim()} (streaming)";

        return new Panel(new Rows(highlightedLines))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };
    }

    private static string ProcessInline(string text)
    {
        var escaped = text.EscapeMarkup();
        
        // Bold: **text**
        escaped = Regex.Replace(escaped, @"\*\*(.*?)\*\*", "[bold]$1[/]");
        
        // Italic: *text* (avoiding cases like URL or bullet)
        escaped = Regex.Replace(escaped, @"(?<!\*)\*(?!\s)(.*?)(?<!\s)\*(?!\*)", "[italic]$1[/]");
        
        // Code inline: `text`
        escaped = Regex.Replace(escaped, @"`(.*?)`", "[olive]$1[/]");
        
        // Links: [text](url)
        escaped = Regex.Replace(escaped, @"\[(.*?)\]\((.*?)\)", "[link=$2 blue]$1[/]");

        return escaped;
    }

    private static string? MapLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        return language.Trim().ToLowerInvariant() switch
        {
            "c#" or "cs" => "csharp",
            "js" => "javascript",
            "ts" => "typescript",
            "sh" => "bash",
            "yml" => "yaml",
            _ => language.Trim().ToLowerInvariant()
        };
    }

    private static string HighlightCodeLine(string line, string language)
    {
        var escaped = line.EscapeMarkup();
        escaped = Regex.Replace(escaped, "\"([^\"]*)\"", "[green]\"$1\"[/]");
        escaped = Regex.Replace(escaped, @"'([^']*)'", "[green]'$1'[/]");

        if (language is "csharp" or "javascript" or "typescript" or "python" or "go" or "rust" or "bash")
        {
            var commentPattern = language switch
            {
                "python" => @"(#.*)$",
                "bash" => @"(#.*)$",
                _ => @"(//.*)$"
            };
            escaped = Regex.Replace(escaped, commentPattern, "[grey]$1[/]");
        }

        IReadOnlyList<string> keywords = language switch
        {
            "csharp" => new[] { "using", "namespace", "public", "private", "protected", "internal", "class", "record", "interface", "enum", "return", "await", "async", "var", "new", "if", "else", "switch", "case", "try", "catch", "finally", "static", "void", "true", "false", "null" },
            "javascript" or "typescript" => new[] { "const", "let", "var", "function", "return", "await", "async", "class", "interface", "type", "if", "else", "switch", "case", "new", "import", "export", "from", "true", "false", "null", "undefined" },
            "python" => new[] { "def", "class", "return", "await", "async", "if", "elif", "else", "try", "except", "finally", "import", "from", "as", "True", "False", "None" },
            "go" => new[] { "func", "package", "import", "return", "if", "else", "switch", "case", "var", "const", "type", "struct", "interface", "go", "defer" },
            "rust" => new[] { "fn", "let", "mut", "pub", "impl", "struct", "enum", "match", "if", "else", "return", "async", "await", "use", "mod", "trait" },
            "bash" => new[] { "if", "then", "else", "fi", "for", "do", "done", "case", "esac", "function", "export" },
            _ => Array.Empty<string>()
        };

        foreach (var keyword in keywords)
        {
            escaped = Regex.Replace(escaped, $@"\b{Regex.Escape(keyword)}\b", $"[blue]{keyword}[/]");
        }

        return escaped;
    }

    private sealed record MarkdownStreamingState(bool HasRichContent, bool InCodeBlock)
    {
        public static MarkdownStreamingState Analyze(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new MarkdownStreamingState(false, false);
            }

            var lines = content.Split('\n');
            var fenceCount = 0;
            var hasHeading = false;
            var hasList = false;
            var hasTable = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    fenceCount++;
                }

                if (line.StartsWith("# ", StringComparison.Ordinal) ||
                    line.StartsWith("## ", StringComparison.Ordinal) ||
                    line.StartsWith("### ", StringComparison.Ordinal))
                {
                    hasHeading = true;
                }

                if (line.StartsWith("- ", StringComparison.Ordinal) ||
                    line.StartsWith("* ", StringComparison.Ordinal) ||
                    Regex.IsMatch(line, @"^\d+\. "))
                {
                    hasList = true;
                }

                if (Regex.IsMatch(line, @"^\|?(?:\s*:?-{3,}:?\s*\|)+\s*:?-{3,}:?\s*\|?$"))
                {
                    hasTable = true;
                }
            }

            var inCodeBlock = fenceCount % 2 == 1;
            var hasRichContent = fenceCount > 0 || hasHeading || hasList || hasTable;
            return new MarkdownStreamingState(hasRichContent, inCodeBlock);
        }
    }
}
