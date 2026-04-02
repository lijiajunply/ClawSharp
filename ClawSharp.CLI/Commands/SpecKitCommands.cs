using System.CommandLine;
using System.Diagnostics;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ClawSharp.CLI.Commands;

public static class SpecKitCommands
{
    public static Command Create(IHost host)
    {
        var command = new Command("speckit", I18n.T("SpecKit.Description"));

        var initCommand = new Command("init", I18n.T("SpecKit.Init.Description"));
        var featureNameArgument = new Argument<string>("name", I18n.T("SpecKit.Init.NameArg"));
        initCommand.AddArgument(featureNameArgument);
        initCommand.SetHandler(async featureName =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await RunInitAsync(Directory.GetCurrentDirectory(), featureName, CancellationToken.None);
                return 0;
            });
        }, featureNameArgument);

        var scaffoldCommand = new Command("scaffold", I18n.T("SpecKit.Scaffold.Description"));
        var planArgument = new Argument<string?>("planPath", () => null, I18n.T("SpecKit.Scaffold.PlanArg"));
        var yesOption = new Option<bool>("--yes", I18n.T("SpecKit.Scaffold.YesOption"));
        scaffoldCommand.AddArgument(planArgument);
        scaffoldCommand.AddOption(yesOption);
        scaffoldCommand.SetHandler(async (planPath, yes) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await RunScaffoldAsync(host, ResolvePlanPath(planPath), skipConfirmation: yes, CancellationToken.None);
                return 0;
            });
        }, planArgument, yesOption);

        var watchCommand = new Command("watch", I18n.T("SpecKit.Watch.Description"));
        watchCommand.AddArgument(planArgument);
        watchCommand.SetHandler(async planPath =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await RunWatchAsync(host, ResolvePlanPath(planPath), CancellationToken.None);
                return 0;
            });
        }, planArgument);

        command.AddCommand(initCommand);
        command.AddCommand(scaffoldCommand);
        command.AddCommand(watchCommand);
        command.SetHandler(ShowOverview);
        return command;
    }

    internal static async Task HandleReplAsync(IHost host, string workingDirectory, string arguments,
        CancellationToken cancellationToken = default)
    {
        var parts = arguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
        var remainder = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (action)
        {
            case "":
                ShowOverview();
                return;
            case "init":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine(I18n.T("SpecKit.Usage.Init"));
                    return;
                }

                await RunInitAsync(workingDirectory, remainder, cancellationToken).ConfigureAwait(false);
                return;
            case "scaffold":
                await RunScaffoldAsync(host,
                    ResolvePlanPath(string.IsNullOrWhiteSpace(remainder) ? null : remainder, workingDirectory),
                    skipConfirmation: false, cancellationToken).ConfigureAwait(false);
                return;
            default:
                AnsiConsole.MarkupLine(I18n.T("SpecKit.UnknownSubcommand", action.EscapeMarkup()));
                ShowOverview();
                return;
        }
    }

    private static async Task RunInitAsync(string workingDirectory, string featureName,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(workingDirectory, ".specify", "scripts", "bash", "create-new-feature.sh");
        if (!File.Exists(scriptPath))
        {
            AnsiConsole.MarkupLine(I18n.T("SpecKit.ScriptMissing", scriptPath.EscapeMarkup()));
            return;
        }

        var result = await RunShellScriptAsync(
            scriptPath,
            $"--json {EscapeShellArgument(featureName)}",
            workingDirectory,
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            AnsiConsole.MarkupLine(I18n.T("SpecKit.InitFailed", result.Error?.EscapeMarkup() ?? I18n.T("Common.UnknownError")));
            return;
        }

        AnsiConsole.MarkupLine(I18n.T("SpecKit.InitSuccess"));
        if (!string.IsNullOrWhiteSpace(result.Value))
        {
            AnsiConsole.WriteLine(result.Value.Trim());
        }
    }

    private static async Task RunScaffoldAsync(IHost host, string planPath, bool skipConfirmation,
        CancellationToken cancellationToken)
    {
        var analyzer = host.Services.GetRequiredService<IScaffoldAnalyzer>();
        var planner = host.Services.GetRequiredService<IPlannerAgent>();

        var analysis = await analyzer.AnalyzePlanAsync(planPath, cancellationToken).ConfigureAwait(false);
        if (!analysis.IsSuccess || analysis.Value is null)
        {
            AnsiConsole.MarkupLine(
                I18n.T("SpecKit.PlanAnalysisFailed", analysis.Error?.EscapeMarkup() ?? I18n.T("Common.UnknownError")));
            return;
        }

        ShowPlanPreview(analysis.Value);

        if (!skipConfirmation && !AnsiConsole.Confirm(I18n.T("SpecKit.ConfirmApply"), false))
        {
            AnsiConsole.MarkupLine(I18n.T("SpecKit.Cancelled"));
            return;
        }

        var result = await planner.ExecuteScaffoldAsync(analysis.Value, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            AnsiConsole.MarkupLine(
                I18n.T("SpecKit.GenerationFailed", result.Error?.EscapeMarkup() ?? I18n.T("Common.UnknownError")));
            return;
        }

        AnsiConsole.MarkupLine(I18n.T("SpecKit.GenerationCompleted"));
    }

    private static async Task RunWatchAsync(IHost host, string planPath, CancellationToken cancellationToken)
    {
        var fullPlanPath = Path.GetFullPath(planPath);
        var directory = Path.GetDirectoryName(fullPlanPath)
                        ?? throw new InvalidOperationException(I18n.T("SpecKit.PlanParentMissing"));
        var fileName = Path.GetFileName(fullPlanPath);

        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastTriggeredAt = DateTimeOffset.MinValue;

        using var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        void Trigger()
        {
            var now = DateTimeOffset.UtcNow;
            if (now - lastTriggeredAt < TimeSpan.FromMilliseconds(500))
            {
                return;
            }

            lastTriggeredAt = now;
            signal.TrySetResult();
        }

        watcher.Changed += (_, _) => Trigger();
        watcher.Created += (_, _) => Trigger();
        watcher.Renamed += (_, _) => Trigger();

        AnsiConsole.MarkupLine(
            I18n.T("SpecKit.Watching", fullPlanPath.EscapeMarkup()));

        while (!cancellationToken.IsCancellationRequested)
        {
            await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            AnsiConsole.MarkupLine(I18n.T("SpecKit.PlanUpdated"));
            await RunScaffoldAsync(host, fullPlanPath, skipConfirmation: false, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void ShowPlanPreview(ScaffoldPlan plan)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(I18n.T("Common.Item"));
        table.AddColumn(I18n.T("Common.Value"));
        table.AddRow(I18n.T("SpecKit.Preview.Feature"), $"{plan.Metadata.FeatureId} / {plan.Metadata.ShortName}");
        table.AddRow(I18n.T("SpecKit.Preview.Branch"), plan.GitBranchToCreate ?? I18n.T("SpecKit.Preview.Skip"));
        table.AddRow(I18n.T("SpecKit.Preview.Directories"), plan.DirectoriesToCreate.Count.ToString());
        table.AddRow(I18n.T("SpecKit.Preview.Files"), plan.FilesToScaffold.Count.ToString());
        table.AddRow(I18n.T("SpecKit.Preview.Tasks"), plan.TasksToGenerate.Count.ToString());
        AnsiConsole.Write(table);

        if (plan.FilesToScaffold.Count > 0)
        {
            IRenderable fileList = new Rows(plan.FilesToScaffold.Take(10)
                .Select(file => new Markup($"[grey]-[/] {file.Path.EscapeMarkup()}"))
                .ToArray<IRenderable>());
            AnsiConsole.Write(new Panel(fileList).Header(I18n.T("SpecKit.Preview.PlannedFiles")));
        }
    }

    private static void ShowOverview()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(I18n.T("Common.Command"));
        table.AddColumn(I18n.T("Common.Description"));
        table.AddRow("/speckit init <name>".EscapeMarkup(), I18n.T("SpecKit.Overview.Init").EscapeMarkup());
        table.AddRow("/speckit scaffold [plan.md]".EscapeMarkup(), I18n.T("SpecKit.Overview.Scaffold").EscapeMarkup());
        table.AddRow("/speckit watch [plan.md]".EscapeMarkup(), I18n.T("SpecKit.Overview.Watch").EscapeMarkup());
        AnsiConsole.Write(table);
    }

    private static string ResolvePlanPath(string? planPath, string? basePath = null)
    {
        if (!string.IsNullOrWhiteSpace(planPath))
        {
            return Path.GetFullPath(Path.IsPathRooted(planPath)
                ? planPath
                : Path.Combine(basePath ?? Directory.GetCurrentDirectory(), planPath));
        }

        var currentDirectory = basePath ?? Directory.GetCurrentDirectory();
        var candidate = Directory.EnumerateFiles(currentDirectory, "plan.md", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return candidate ?? Path.Combine(currentDirectory, "plan.md");
    }

    private static async Task<OperationResult<string>> RunShellScriptAsync(
        string scriptPath,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("/bin/bash", $"{EscapeShellArgument(scriptPath)} {arguments}")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return OperationResult<string>.Failure(I18n.T("SpecKit.ShellStartFailed"));
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return process.ExitCode == 0
            ? OperationResult<string>.Success(output)
            : OperationResult<string>.Failure(string.IsNullOrWhiteSpace(error) ? I18n.T("SpecKit.ShellFailed") : error.Trim());
    }

    private static string EscapeShellArgument(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}
