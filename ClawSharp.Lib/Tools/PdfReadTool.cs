using System.Text.Json;
using UglyToad.PdfPig;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// A tool that extracts text from PDF documents.
/// </summary>
public sealed class PdfReadTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "pdf_read",
        "Extract text from PDF documents.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "The PDF file path." },
                pages = new { type = "array", items = new { type = "integer" }, description = "Optional: Specific page numbers to extract (1-indexed)." }
            },
            required = new[] { "path" }
        }),
        null,
        ToolCapability.FileRead);

    /// <inheritdoc />
    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var path = arguments.GetProperty("path").GetString() ?? string.Empty;
        var specificPages = arguments.TryGetProperty("pages", out var p) ? p.EnumerateArray().Select(x => x.GetInt32()).ToArray() : null;

        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return Task.FromResult(ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path }));
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(ToolInvocationResult.Failure(Definition.Name, $"File not found: {path}"));
        }

        try
        {
            using var document = PdfDocument.Open(fullPath);
            var results = new List<object>();

            // If specific pages are requested, use them, otherwise extract all
            var pagesToRead = specificPages ?? Enumerable.Range(1, document.NumberOfPages).ToArray();

            foreach (var pageNum in pagesToRead)
            {
                if (pageNum >= 1 && pageNum <= document.NumberOfPages)
                {
                    var page = document.GetPage(pageNum);
                    var pageText = ExtractStructuredText(page);
                    results.Add(new { page = pageNum, text = pageText });
                }
            }

            return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, pages = results })));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolInvocationResult.Failure(Definition.Name, $"Failed to read PDF: {ex.Message}"));
        }
    }

    private static string ExtractStructuredText(UglyToad.PdfPig.Content.Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0) return string.Empty;

        // Group words by their vertical position (Bottom) with a small tolerance (e.g., 2.0 units)
        var lines = new List<List<UglyToad.PdfPig.Content.Word>>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            var added = false;
            foreach (var line in lines)
            {
                // If the word's vertical center is close to the line's vertical center, it's the same line
                var lineBottom = line.Average(w => w.BoundingBox.Bottom);
                if (Math.Abs(word.BoundingBox.Bottom - lineBottom) < 2.0)
                {
                    line.Add(word);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                lines.Add(new List<UglyToad.PdfPig.Content.Word> { word });
            }
        }

        var sb = new System.Text.StringBuilder();
        foreach (var line in lines.OrderByDescending(l => l.Average(w => w.BoundingBox.Bottom)))
        {
            var sortedLine = line.OrderBy(w => w.BoundingBox.Left).ToList();
            for (int i = 0; i < sortedLine.Count; i++)
            {
                var word = sortedLine[i];
                sb.Append(word.Text);
                
                if (i < sortedLine.Count - 1)
                {
                    var nextWord = sortedLine[i + 1];
                    var gap = nextWord.BoundingBox.Left - word.BoundingBox.Right;
                    
                    // Add spaces based on gap width. Heuristic: 
                    // if gap > 3x the space between normal words, it might be a new column
                    if (gap > 5.0) 
                    {
                        sb.Append("  "); // Extra space for potential columns
                    }
                    else if (gap > 0.5)
                    {
                        sb.Append(" ");
                    }
                }
            }
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }
}
