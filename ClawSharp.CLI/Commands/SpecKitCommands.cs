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
        var command = new Command("speckit", "Run SpecKit workflows from the CLI");

        var initCommand = new Command("init", "Create a new SpecKit feature scaffold");
        var featureNameArgument = new Argument<string>("name", "Feature description");
        initCommand.AddArgument(featureNameArgument);
        initCommand.SetHandler(async featureName =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await RunInitAsync(Directory.GetCurrentDirectory(), featureName, CancellationToken.None);
                return 0;
            });
        }, featureNameArgument);

        var scaffoldCommand = new Command("scaffold", "Analyze plan.md and generate scaffolded files");
        var planArgument = new Argument<string?>("planPath", () => null, "Optional path to plan.md");
        var yesOption = new Option<bool>("--yes", "Apply without interactive confirmation");
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

        var watchCommand = new Command("watch", "Watch a plan.md file and prompt before scaffold generation");
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
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /speckit init <feature description>");
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
                AnsiConsole.MarkupLine($"[yellow]Unknown /speckit subcommand:[/] {action.EscapeMarkup()}");
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
            AnsiConsole.MarkupLine($"[red]SpecKit script not found:[/] {scriptPath.EscapeMarkup()}");
            return;
        }

        var result = await RunShellScriptAsync(
            scriptPath,
            $"--json {EscapeShellArgument(featureName)}",
            workingDirectory,
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]SpecKit init failed:[/] {result.Error?.EscapeMarkup() ?? "Unknown error"}");
            return;
        }

        AnsiConsole.MarkupLine("[green]SpecKit feature created successfully.[/]");
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
                $"[red]Plan analysis failed:[/] {analysis.Error?.EscapeMarkup() ?? "Unknown error"}");
            return;
        }

        ShowPlanPreview(analysis.Value);

        if (!skipConfirmation && !AnsiConsole.Confirm("Apply this scaffold plan?", false))
        {
            AnsiConsole.MarkupLine("[yellow]Scaffold generation cancelled.[/]");
            return;
        }

        var result = await planner.ExecuteScaffoldAsync(analysis.Value, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            AnsiConsole.MarkupLine(
                $"[red]Scaffold generation failed:[/] {result.Error?.EscapeMarkup() ?? "Unknown error"}");
            return;
        }

        AnsiConsole.MarkupLine("[green]Scaffold generation completed.[/]");
    }

    private static async Task RunWatchAsync(IHost host, string planPath, CancellationToken cancellationToken)
    {
        var fullPlanPath = Path.GetFullPath(planPath);
        var directory = Path.GetDirectoryName(fullPlanPath)
                        ?? throw new InvalidOperationException("plan.md must have a parent directory.");
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
            $"[grey]Watching[/] {fullPlanPath.EscapeMarkup()} [grey]for changes. Press Ctrl+C to stop.[/]");

        while (!cancellationToken.IsCancellationRequested)
        {
            await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            AnsiConsole.MarkupLine("[blue]Detected plan update.[/]");
            await RunScaffoldAsync(host, fullPlanPath, skipConfirmation: false, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void ShowPlanPreview(ScaffoldPlan plan)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Item");
        table.AddColumn("Value");
        table.AddRow("Feature", $"{plan.Metadata.FeatureId} / {plan.Metadata.ShortName}");
        table.AddRow("Branch", plan.GitBranchToCreate ?? "(skip)");
        table.AddRow("Directories", plan.DirectoriesToCreate.Count.ToString());
        table.AddRow("Files", plan.FilesToScaffold.Count.ToString());
        table.AddRow("Tasks", plan.TasksToGenerate.Count.ToString());
        AnsiConsole.Write(table);

        if (plan.FilesToScaffold.Count > 0)
        {
            IRenderable fileList = new Rows(plan.FilesToScaffold.Take(10)
                .Select(file => new Markup($"[grey]-[/] {file.Path.EscapeMarkup()}"))
                .ToArray<IRenderable>());
            AnsiConsole.Write(new Panel(fileList).Header("Planned Files"));
        }
    }

    private static void ShowOverview()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.AddRow("/speckit init <name>", "Create a new feature spec directory");
        table.AddRow("/speckit scaffold [plan.md]", "Analyze plan.md, preview changes, and scaffold files");
        table.AddRow("speckit watch [plan.md]", "Watch plan.md and prompt before scaffold generation");
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
            return OperationResult<string>.Failure("Failed to start shell script.");
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return process.ExitCode == 0
            ? OperationResult<string>.Success(output)
            : OperationResult<string>.Failure(string.IsNullOrWhiteSpace(error) ? "Shell script failed." : error.Trim());
    }

    private static string EscapeShellArgument(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}