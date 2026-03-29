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

    public Markdown(string content)
    {
        _content = content ?? string.Empty;
    }

    /// <summary>
    /// Updates the content for streaming scenarios.
    /// </summary>
    public void Update(string content)
    {
        _content = content ?? string.Empty;
    }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(maxWidth, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var result = new List<Segment>();
        var lines = _content.Split('\n');
        bool inCodeBlock = false;
        bool inTable = false;
        var tableRows = new List<string[]>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Code Blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inTable) { RenderTable(result, tableRows, options, maxWidth); inTable = false; }
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                var markup = $"[grey]{line.EscapeMarkup()}[/]";
                result.AddRange(((IRenderable)new Markup(markup)).Render(options, maxWidth));
                result.Add(new Segment(Environment.NewLine));
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
}
