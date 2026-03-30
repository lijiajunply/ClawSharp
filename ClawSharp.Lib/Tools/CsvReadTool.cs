using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// A tool that reads CSV files with support for pagination and structured output.
/// </summary>
public sealed class CsvReadTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "csv_read",
        "Read a CSV file with pagination support.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "The CSV file path." },
                limit = new { type = "integer", @default = 50, description = "Number of rows to read." },
                offset = new { type = "integer", @default = 0, description = "Number of rows to skip." },
                has_header = new { type = "boolean", @default = true, description = "Whether the file has a header row." }
            },
            required = new[] { "path" }
        }),
        null,
        ToolCapability.FileRead);

    /// <inheritdoc />
    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var path = arguments.GetProperty("path").GetString() ?? string.Empty;
        var limit = arguments.TryGetProperty("limit", out var l) ? l.GetInt32() : 50;
        var offset = arguments.TryGetProperty("offset", out var o) ? o.GetInt32() : 0;
        var hasHeader = arguments.TryGetProperty("has_header", out var h) ? h.GetBoolean() : true;

        var check = ToolSecurity.EnsurePathAllowed(context.WorkspaceRoot, path, context.Permissions.AllowedReadRoots, write: false);
        if (!check.IsSuccess)
        {
            return ToolSecurity.CreateApprovalOrDenied(Definition, context, check.Error!, new { path });
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(context.WorkspaceRoot, path));
        if (!File.Exists(fullPath))
        {
            return ToolInvocationResult.Failure(Definition.Name, $"File not found: {path}");
        }

        try
        {
            using var reader = new StreamReader(fullPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = hasHeader });

            var data = new List<dynamic>();
            int currentRow = 0;
            int count = 0;

            if (await csv.ReadAsync())
            {
                if (hasHeader)
                {
                    csv.ReadHeader();
                }

                while (await csv.ReadAsync() && count < limit)
                {
                    if (currentRow >= offset)
                    {
                        data.Add(csv.GetRecord<dynamic>()!);
                        count++;
                    }
                    currentRow++;
                }
            }

            return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { path = fullPath, data, total_read = count }));
        }
        catch (Exception ex)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"Failed to read CSV: {ex.Message}");
        }
    }
}
