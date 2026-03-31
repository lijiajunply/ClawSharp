using System.Text;
using System.Text.RegularExpressions;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Markdown;

namespace ClawSharp.Lib.Projects;

internal sealed class ScaffoldAnalyzer(MarkdownSectionParser sectionParser) : IScaffoldAnalyzer
{
    private static readonly Regex BranchPattern = new(@"\*\*Branch\*\*:\s*`?(?<branch>[^`\r\n|]+)`?", RegexOptions.Compiled);
    private static readonly Regex FeatureFolderPattern = new(@"^(?<id>\d+)-(?<short>.+)$", RegexOptions.Compiled);

    public async Task<OperationResult<ScaffoldPlan>> AnalyzePlanAsync(string planPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(planPath))
            {
                return OperationResult<ScaffoldPlan>.Failure("Plan path must not be empty.");
            }

            var fullPlanPath = Path.GetFullPath(planPath);
            if (!File.Exists(fullPlanPath))
            {
                return OperationResult<ScaffoldPlan>.Failure($"Plan file '{fullPlanPath}' does not exist.");
            }

            var markdown = await File.ReadAllTextAsync(fullPlanPath, cancellationToken).ConfigureAwait(false);
            var featureRoot = Path.GetDirectoryName(fullPlanPath)
                ?? throw new ValidationException("Plan file must live inside a feature directory.");

            var metadata = BuildFeatureMetadata(fullPlanPath, featureRoot, markdown);
            var sourceCodeFence = sectionParser.GetFenceContent(markdown, "Source Code").FirstOrDefault() ?? string.Empty;
            var files = ParseScaffoldFiles(sourceCodeFence);
            var directories = files
                .Select(file => Path.GetDirectoryName(file.Path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();

            var tasks = ExtractMilestones(markdown, files);

            return OperationResult<ScaffoldPlan>.Success(
                new ScaffoldPlan(
                    metadata,
                    metadata.BranchName,
                    directories,
                    files,
                    tasks));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ValidationException)
        {
            return OperationResult<ScaffoldPlan>.Failure(ex.Message);
        }
    }

    private FeatureMetadata BuildFeatureMetadata(string planPath, string featureRoot, string markdown)
    {
        var featureFolder = Path.GetFileName(featureRoot);
        var folderMatch = FeatureFolderPattern.Match(featureFolder);
        var featureId = folderMatch.Success ? folderMatch.Groups["id"].Value : "000";
        var shortName = folderMatch.Success ? folderMatch.Groups["short"].Value : featureFolder;

        var branchName = BranchPattern.Match(markdown) is { Success: true } match
            ? match.Groups["branch"].Value.Trim()
            : $"{featureId}-{shortName}";

        var status = Regex.Match(markdown, @"\*\*状态\*\*:\s*(?<status>[^\r\n]+)", RegexOptions.Compiled) is { Success: true } statusMatch
            ? statusMatch.Groups["status"].Value.Trim()
            : "Draft";

        return new FeatureMetadata(
            featureId,
            shortName,
            branchName,
            status,
            featureRoot,
            planPath,
            Path.Combine(featureRoot, "tasks.md"));
    }

    private static IReadOnlyList<ScaffoldFile> ParseScaffoldFiles(string sourceTree)
    {
        if (string.IsNullOrWhiteSpace(sourceTree))
        {
            return [];
        }

        var results = new List<ScaffoldFile>();
        var stack = new List<string>();

        foreach (var rawLine in sourceTree.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = StripComment(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var normalized = line.TrimStart(' ', '│', '├', '└', '─', '\t').Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            var level = Math.Max(0, line.IndexOf(normalized, StringComparison.Ordinal)) / 4;

            var isDirectory = normalized.EndsWith("/", StringComparison.Ordinal);
            var entryName = normalized.TrimEnd('/');
            while (stack.Count > level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            if (isDirectory)
            {
                if (stack.Count == level)
                {
                    stack.Add(entryName);
                }
                else if (stack.Count > level)
                {
                    stack[level] = entryName;
                }

                continue;
            }

            var segments = stack.Concat([entryName]).ToArray();
            var relativePath = string.Join(Path.DirectorySeparatorChar, segments);
            results.Add(new ScaffoldFile(
                relativePath,
                InferFileKind(relativePath),
                CreatePlaceholder(relativePath)));
        }

        return results
            .DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractMilestones(string markdown, IReadOnlyList<ScaffoldFile> files)
    {
        var items = new List<string>();
        var parser = new MarkdownSectionParser();
        foreach (var heading in new[]
                 {
                     "Implementation Strategy",
                     "Incremental Delivery",
                     "Milestones",
                     "Tasks",
                     "Next Steps"
                 })
        {
            items.AddRange(parser.GetBulletItems(markdown, heading));
        }

        if (items.Count > 0)
        {
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return files
            .Select(file => $"Create placeholder for `{file.Path}`")
            .ToArray();
    }

    private static string StripComment(string line)
    {
        var commentIndex = line.IndexOf('#');
        return commentIndex >= 0 ? line[..commentIndex].TrimEnd() : line.TrimEnd();
    }

    private static string InferFileKind(string relativePath)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => relativePath.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ? "CSharpTest" : "CSharpClass",
            ".md" => "Markdown",
            ".json" => "Json",
            ".yaml" or ".yml" => "Yaml",
            _ => "File"
        };
    }

    private static string CreatePlaceholder(string relativePath)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => CreateCSharpPlaceholder(relativePath),
            ".md" => $"# {Path.GetFileNameWithoutExtension(relativePath)}{Environment.NewLine}",
            ".json" => "{}" + Environment.NewLine,
            ".yaml" or ".yml" => "# Generated by PlannerAgent" + Environment.NewLine,
            _ => string.Empty
        };
    }

    private static string CreateCSharpPlaceholder(string relativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        var namespaceSegments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Reverse()
            .Skip(1)
            .Reverse()
            .Where(segment => !string.Equals(segment, ".", StringComparison.Ordinal))
            .Select(segment => Regex.Replace(segment, "[^a-zA-Z0-9_]", string.Empty))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        var @namespace = namespaceSegments.Length == 0
            ? "Generated"
            : string.Join('.', namespaceSegments);

        if (fileName.EndsWith("Tests", StringComparison.Ordinal))
        {
            return $$"""
            namespace {{@namespace}};

            public sealed class {{fileName}}
            {
                [Fact]
                public void Placeholder()
                {
                }
            }
            """;
        }

        var kind = fileName.StartsWith("I", StringComparison.Ordinal) && fileName.Length > 1 && char.IsUpper(fileName[1])
            ? "interface"
            : "class";

        return kind == "interface"
            ? $$"""
               namespace {{@namespace}};

               public interface {{fileName}}
               {
               }
               """
            : $$"""
               namespace {{@namespace}};

               public sealed class {{fileName}}
               {
               }
               """;
    }
}
