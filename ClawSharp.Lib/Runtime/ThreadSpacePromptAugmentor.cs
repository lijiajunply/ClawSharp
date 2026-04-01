using System.Text;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Runtime;

internal static class ThreadSpacePromptAugmentor
{
    public static async Task<string> BuildAsync(
        ThreadSpaceRecord threadSpace,
        string workspaceRoot,
        ThreadSpacePromptOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(threadSpace.BoundFolderPath))
        {
            return string.Empty;
        }

        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            return string.Empty;
        }

        var sections = new List<string>
        {
            BuildWorkspaceInstructionSection(root)
        };

        var documentSection = await BuildProjectDocumentSectionAsync(root, options, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(documentSection))
        {
            sections.Add(documentSection);
        }

        return string.Join("\n\n", sections);
    }

    private static string BuildWorkspaceInstructionSection(string workspaceRoot)
    {
        return
            "[ThreadSpace Workspace]\n" +
            $"You are operating inside a bound project workspace at: {workspaceRoot}\n" +
            "When the user's request can be completed inside this workspace, prefer taking concrete actions instead of only describing them.\n" +
            "- Inspect the existing files before making assumptions.\n" +
            "- Create or edit files directly when that would fulfill the request.\n" +
            "- Run commands, builds, or tests when they help verify the result.\n" +
            "- Keep explanations concise and centered on the work you performed.\n" +
            "- Only stay at the discussion or planning level when the user explicitly asks for that.";
    }

    private static async Task<string> BuildProjectDocumentSectionAsync(
        string workspaceRoot,
        ThreadSpacePromptOptions options,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var consumedChars = 0;
        var foundAny = false;
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documentCandidates = options.ProjectDocumentCandidates
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        foreach (var fileName in documentCandidates)
        {
            var path = Path.Combine(workspaceRoot, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seenPaths.Add(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var remainingBudget = options.MaxCombinedDocumentChars - consumedChars;
            if (remainingBudget <= 0)
            {
                break;
            }

            var excerpt = TrimForPrompt(content, Math.Min(options.MaxDocumentChars, remainingBudget));
            if (string.IsNullOrWhiteSpace(excerpt))
            {
                continue;
            }

            if (!foundAny)
            {
                builder.AppendLine("[Project Documents]");
                builder.AppendLine("Treat these files as project guidance that should influence your decisions inside this ThreadSpace.");
                foundAny = true;
            }

            builder.AppendLine($"File: {Path.GetFileName(fullPath)}");
            builder.AppendLine("```md");
            builder.AppendLine(excerpt);
            builder.AppendLine("```");

            consumedChars += excerpt.Length;
        }

        return builder.ToString().Trim();
    }

    private static string TrimForPrompt(string content, int maxChars)
    {
        var normalized = content.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return normalized[..maxChars].TrimEnd() + "\n...[truncated]";
    }
}
