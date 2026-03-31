using System.Text;
using System.Text.RegularExpressions;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Markdown;

/// <summary>
/// 提供基于 Markdown 标题的区块提取能力。
/// </summary>
internal sealed class MarkdownSectionParser
{
    private static readonly Regex HeadingPattern = new(
        @"^(?<hashes>#{1,6})\s+(?<title>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public string GetSection(string markdown, string heading) =>
        TryGetSection(markdown, heading, out var section)
            ? section
            : throw new ValidationException($"Markdown section '{heading}' was not found.");

    public bool TryGetSection(string markdown, string heading, out string section)
    {
        section = string.Empty;

        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(heading))
        {
            return false;
        }

        var matches = HeadingPattern.Matches(markdown);
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var title = NormalizeHeading(match.Groups["title"].Value);
            if (!string.Equals(title, NormalizeHeading(heading), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var currentLevel = match.Groups["hashes"].Value.Length;
            var contentStart = match.Index + match.Length;
            var contentEnd = markdown.Length;

            for (var nextIndex = index + 1; nextIndex < matches.Count; nextIndex++)
            {
                var nextMatch = matches[nextIndex];
                var nextLevel = nextMatch.Groups["hashes"].Value.Length;
                if (nextLevel <= currentLevel)
                {
                    contentEnd = nextMatch.Index;
                    break;
                }
            }

            section = markdown[contentStart..contentEnd].Trim();
            return true;
        }

        return false;
    }

    public IReadOnlyList<string> GetBulletItems(string markdown, string heading)
    {
        if (!TryGetSection(markdown, heading, out var section))
        {
            return [];
        }

        var items = new List<string>();
        using var reader = new StringReader(section);
        string? line;
        var inFence = false;

        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                items.Add(trimmed[2..].Trim());
                continue;
            }

            if (char.IsDigit(trimmed[0]))
            {
                var markerIndex = trimmed.IndexOf(". ", StringComparison.Ordinal);
                if (markerIndex > 0)
                {
                    items.Add(trimmed[(markerIndex + 2)..].Trim());
                }
            }
        }

        return items;
    }

    public IReadOnlyList<string> GetFenceContent(string markdown, string heading)
    {
        if (!TryGetSection(markdown, heading, out var section))
        {
            return [];
        }

        var fences = new List<string>();
        var buffer = new StringBuilder();
        var inFence = false;

        foreach (var line in section.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inFence)
                {
                    fences.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }

                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                buffer.AppendLine(line);
            }
        }

        return fences;
    }

    private static string NormalizeHeading(string heading) =>
        Regex.Replace(
                heading.Trim().TrimEnd(':').Replace("`", string.Empty, StringComparison.Ordinal),
                @"\s+\([^)]*\)$",
                string.Empty)
            .Trim();
}
