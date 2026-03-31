using System.Diagnostics;
using System.Text;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Runtime;

namespace ClawSharp.Lib.Agents;

/// <summary>
/// 负责执行脚手架计划的代理。
/// </summary>
public interface IPlannerAgent
{
    /// <summary>
    /// 根据分析后的脚手架计划创建目录、文件和任务内容。
    /// </summary>
    /// <param name="plan">要执行的脚手架计划。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果。</returns>
    Task<OperationResult> ExecuteScaffoldAsync(ScaffoldPlan plan, CancellationToken cancellationToken = default);
}

internal sealed class PlannerAgent(IFeatureContextRepository featureContexts) : IPlannerAgent
{
    public async Task<OperationResult> ExecuteScaffoldAsync(ScaffoldPlan plan, CancellationToken cancellationToken = default)
    {
        try
        {
            var repoRoot = FindRepositoryRoot(plan.Metadata.FeatureRootPath);

            if (!string.IsNullOrWhiteSpace(plan.GitBranchToCreate) && repoRoot is not null)
            {
                var branchResult = await EnsureBranchAsync(repoRoot, plan.GitBranchToCreate!, cancellationToken).ConfigureAwait(false);
                if (!branchResult.IsSuccess)
                {
                    return branchResult;
                }
            }

            foreach (var directory in plan.DirectoriesToCreate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(repoRoot ?? Directory.GetCurrentDirectory(), directory));
            }

            foreach (var file in plan.FilesToScaffold)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.Combine(repoRoot ?? Directory.GetCurrentDirectory(), file.Path);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(fullPath))
                {
                    continue;
                }

                await File.WriteAllTextAsync(fullPath, file.InitialContent ?? string.Empty, cancellationToken).ConfigureAwait(false);
            }

            await UpdateTasksFileAsync(plan.Metadata.TasksPath, plan.TasksToGenerate, cancellationToken).ConfigureAwait(false);
            await featureContexts.UpsertAsync(
                new FeatureContext(
                    plan.Metadata.FeatureId,
                    "Implementation",
                    true,
                    ComputeChecksum(plan.Metadata.PlanPath),
                    plan.GitBranchToCreate,
                    plan.Metadata.FeatureRootPath),
                cancellationToken).ConfigureAwait(false);

            return OperationResult.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ValidationException)
        {
            return OperationResult.Failure(ex.Message);
        }
    }

    private static async Task<OperationResult> EnsureBranchAsync(string repositoryRoot, string branchName, CancellationToken cancellationToken)
    {
        var currentBranch = await RunGitWithOutputAsync(repositoryRoot, "branch --show-current", cancellationToken).ConfigureAwait(false);
        if (!currentBranch.IsSuccess)
        {
            return OperationResult.Failure(currentBranch.Error ?? "Failed to inspect current branch.");
        }

        if (string.Equals(currentBranch.Value?.Trim(), branchName, StringComparison.Ordinal))
        {
            return OperationResult.Success();
        }

        var existingBranches = await RunGitWithOutputAsync(repositoryRoot, "branch --list", cancellationToken).ConfigureAwait(false);
        if (!existingBranches.IsSuccess)
        {
            return OperationResult.Failure(existingBranches.Error ?? "Failed to inspect existing branches.");
        }

        var containsBranch = existingBranches.Value?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('*', ' '))
            .Any(line => string.Equals(line, branchName, StringComparison.Ordinal)) == true;

        return containsBranch
            ? await RunGitAsync(repositoryRoot, $"checkout {EscapeArgument(branchName)}", cancellationToken).ConfigureAwait(false)
            : await RunGitAsync(repositoryRoot, $"checkout -b {EscapeArgument(branchName)}", cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateTasksFileAsync(string tasksPath, IReadOnlyList<string> tasks, CancellationToken cancellationToken)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(tasksPath)!);

        const string startMarker = "<!-- planner-generated:start -->";
        const string endMarker = "<!-- planner-generated:end -->";

        var generatedSection = BuildGeneratedSection(tasks, startMarker, endMarker);
        if (!File.Exists(tasksPath))
        {
            await File.WriteAllTextAsync(tasksPath, generatedSection, cancellationToken).ConfigureAwait(false);
            return;
        }

        var existing = await File.ReadAllTextAsync(tasksPath, cancellationToken).ConfigureAwait(false);
        var startIndex = existing.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = existing.IndexOf(endMarker, StringComparison.Ordinal);

        string updated;
        if (startIndex >= 0 && endIndex > startIndex)
        {
            updated = existing[..startIndex] + generatedSection + existing[(endIndex + endMarker.Length)..];
        }
        else
        {
            var separator = existing.EndsWith(Environment.NewLine, StringComparison.Ordinal) ? string.Empty : Environment.NewLine + Environment.NewLine;
            updated = existing + separator + generatedSection;
        }

        await File.WriteAllTextAsync(tasksPath, updated, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildGeneratedSection(IReadOnlyList<string> tasks, string startMarker, string endMarker)
    {
        var builder = new StringBuilder();
        builder.AppendLine(startMarker);
        builder.AppendLine("## Planner Generated Tasks");
        builder.AppendLine();
        foreach (var task in tasks.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- [ ] ");
            builder.AppendLine(task);
        }

        builder.AppendLine(endMarker);
        return builder.ToString();
    }

    private static string ComputeChecksum(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string? FindRepositoryRoot(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static async Task<OperationResult> RunGitAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var result = await RunGitWithOutputAsync(workingDirectory, arguments, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? OperationResult.Success()
            : OperationResult.Failure(result.Error ?? "Git command failed.");
    }

    private static async Task<OperationResult<string>> RunGitWithOutputAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return OperationResult<string>.Failure("Failed to start git process.");
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return process.ExitCode == 0
            ? OperationResult<string>.Success(standardOutput)
            : OperationResult<string>.Failure(string.IsNullOrWhiteSpace(standardError) ? $"Git command '{arguments}' failed." : standardError.Trim());
    }

    private static string EscapeArgument(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}
