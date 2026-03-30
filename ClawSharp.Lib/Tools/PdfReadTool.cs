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
                    results.Add(new { page = pageNum, text = page.Text });
                }
            }

            return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, pages = results })));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolInvocationResult.Failure(Definition.Name, $"Failed to read PDF: {ex.Message}"));
        }
    }
}
