namespace ClawSharp.Lib.Markdown;

/// <summary>
/// Detects whether Markdown text contains at least one complete fenced code block.
/// </summary>
public static class MarkdownCodeFenceDetector
{
    /// <summary>
    /// Returns <see langword="true"/> when the content contains both an opening and closing fence.
    /// </summary>
    public static bool ContainsFencedCodeBlock(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return false;
        }

        var inFence = false;
        char? fenceChar = null;

        foreach (var rawLine in EnumerateLines(markdown))
        {
            var line = rawLine.TrimStart();
            if (line.Length < 3)
            {
                continue;
            }

            var marker = line[0];
            if ((marker != '`' && marker != '~') || !line.StartsWith(new string(marker, 3), StringComparison.Ordinal))
            {
                continue;
            }

            if (!inFence)
            {
                inFence = true;
                fenceChar = marker;
                continue;
            }

            if (fenceChar == marker)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateLines(string markdown)
    {
        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }
}
