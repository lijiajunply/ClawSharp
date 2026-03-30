using System.Text.Json;
using LibGit2Sharp;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// A tool that provides Git operations like status, log, and diff.
/// </summary>
public sealed class GitOpsTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "git_ops",
        "Perform Git operations (status, log, diff) in the local repository.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new
            {
                operation = new { type = "string", @enum = new[] { "status", "log", "diff" }, description = "The Git operation to perform." },
                target = new { type = "string", description = "Optional: Target for the operation (e.g., branch name, commit hash, file path)." },
                limit = new { type = "integer", @default = 10, description = "Optional: Maximum entries for 'log'." }
            },
            required = new[] { "operation" }
        }),
        null,
        ToolCapability.VersionControl);

    /// <inheritdoc />
    public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var operation = arguments.GetProperty("operation").GetString() ?? string.Empty;
        var target = arguments.TryGetProperty("target", out var t) ? t.GetString() : null;
        var limit = arguments.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

        // Ensure we are in a Git repo within WorkspaceRoot
        if (!Repository.IsValid(context.WorkspaceRoot))
        {
            return Task.FromResult(ToolInvocationResult.Failure(Definition.Name, "Workspace root is not a valid Git repository."));
        }

        try
        {
            using var repo = new Repository(context.WorkspaceRoot);

            object result = operation switch
            {
                "status" => GetStatus(repo),
                "log" => GetLog(repo, limit),
                "diff" => GetDiff(repo, target),
                _ => throw new ArgumentException("Unsupported Git operation.")
            };

            return Task.FromResult(ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(result)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolInvocationResult.Failure(Definition.Name, $"Git operation failed: {ex.Message}"));
        }
    }

    private static object GetStatus(IRepository repo)
    {
        var status = repo.RetrieveStatus();
        var files = status
            .Select(entry => new { path = entry.FilePath, state = entry.State.ToString() })
            .ToArray();

        return new { files, is_clean = !status.IsDirty };
    }

    private static object GetLog(IRepository repo, int limit)
    {
        var logs = repo.Commits
            .Take(limit)
            .Select(c => new { id = c.Sha, author = c.Author.Name, message = c.MessageShort, date = c.Author.When.ToString("o") })
            .ToArray();

        return new { logs };
    }

    private static object GetDiff(IRepository repo, string? target)
    {
        // Simple diff implementation: diff against current head or target
        if (string.IsNullOrEmpty(target))
        {
            var diff = repo.Diff.Compare<TreeChanges>();
            return new { diff = diff.Select(c => new { path = c.Path, status = c.Status.ToString() }).ToArray() };
        }

        // Advanced: Diff against commit or branch
        var commit = repo.Lookup<Commit>(target);
        if (commit != null)
        {
            var diff = repo.Diff.Compare<TreeChanges>(commit.Tree, repo.Head.Tip.Tree);
             return new { target, diff = diff.Select(c => new { path = c.Path, status = c.Status.ToString() }).ToArray() };
        }

        return new { error = "Target commit or branch not found for diff." };
    }
}
