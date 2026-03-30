using System.CommandLine;
using System.Diagnostics;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Markdown;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ChatCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("chat", "Start a new REPL session with an agent");
        var agentIdArg = new Argument<string?>("agent-id", () => null, "The ID of the agent to chat with (optional)");
        command.AddArgument(agentIdArg);

        command.SetHandler(async agentId =>
        {
            await RunAsync(host, agentId);
        }, agentIdArg);

        return command;
    }

    public static async Task<int> RunAsync(IHost host, string? agentId)
    {
        return await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
        {
            var runtime = host.Services.GetRequiredService<IClawRuntime>();
            await runtime.InitializeAsync();

            var kernel = host.Services.GetRequiredService<IClawKernel>();
            var options = host.Services.GetRequiredService<ClawOptions>();
            var currentThreadSpace = await kernel.ThreadSpaces.GetGlobalAsync();
            var finalAgentId = ResolveAgentId(agentId, kernel, options);
            var session = await runtime.StartSessionAsync(new StartSessionRequest(finalAgentId, currentThreadSpace.ThreadSpaceId));

            var state = new ReplState(runtime, kernel, options)
            {
                AgentId = finalAgentId,
                CurrentThreadSpace = currentThreadSpace,
                Session = session,
                SessionId = session.Record.SessionId,
                PromptHandler = CreatePromptHandler(kernel, options)
            };

            ShowWelcomeHeader(finalAgentId, currentThreadSpace.Name);

            var sessionsInSpace = await kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpace.ThreadSpaceId);
            if (sessionsInSpace.Count > 1)
            {
                AnsiConsole.MarkupLine("[grey]Tip: type /resume to continue last conversation.[/]");
            }
            AnsiConsole.WriteLine();

            while (true)
            {
                var input = await state.PromptHandler.AskAsync(GetPrompt(state.CurrentThreadSpace));
                var trimmedInput = input.Trim();
                if (string.IsNullOrWhiteSpace(trimmedInput))
                {
                    continue;
                }

                if (trimmedInput.StartsWith("/", StringComparison.Ordinal))
                {
                    var commandResult = await TryHandleCommandAsync(state, trimmedInput);
                    if (commandResult.ExitRequested)
                    {
                        break;
                    }

                    if (commandResult.SubmittedInput is null)
                    {
                        continue;
                    }

                    trimmedInput = commandResult.SubmittedInput.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedInput))
                    {
                        continue;
                    }
                }

                await RunTurnAsync(state, trimmedInput);
            }

            return 0;
        });
    }

    private static ReplPrompt CreatePromptHandler(IClawKernel kernel, ClawOptions options)
    {
        var promptHandler = new ReplPrompt();
        promptHandler.AddDefaultSuggestions();
        promptHandler.AddSuggestions(kernel.Agents.GetAll().Select(a => a.Id));

        var historyDir = Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath);
        Directory.CreateDirectory(historyDir);
        promptHandler.LoadHistory(Path.Combine(historyDir, "cli_history.txt"));

        return promptHandler;
    }

    private static string ResolveAgentId(string? requestedAgentId, IClawKernel kernel, ClawOptions options)
    {
        var finalAgentId = requestedAgentId;
        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            finalAgentId = options.Agents.DefaultAgentId;
        }

        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            finalAgentId = kernel.Agents.GetAll().FirstOrDefault()?.Id;
        }

        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            throw new InvalidOperationException("No agents found in the registry and no default agent is configured.");
        }

        return finalAgentId;
    }

    private static async Task<CommandDispatchResult> TryHandleCommandAsync(ReplState state, string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var arguments = parts.Length > 1 ? parts[1] : string.Empty;

        return command switch
        {
            "/exit" or "/quit" => CommandDispatchResult.Exit(),
            "/help" => HandleHelp(),
            "/clear" => HandleClear(),
            "/new" => await HandleNewSessionAsync(state),
            "/resume" => await HandleResumeAsync(state),
            "/sessions" => await HandleSessionsAsync(state, arguments),
            "/tools" => await HandleToolsAsync(state),
            "/cd" or "/home" => await HandleThreadSpaceSwitchAsync(state, command, arguments),
            "/init" => await HandleInitAsync(state),
            "/init-proj" => await HandleInitProjectAsync(state),
            "/paste" => await HandlePasteAsync(),
            "/edit" => await HandleEditAsync(),
            _ => HandleUnknownCommand(command)
        };
    }

    private static CommandDispatchResult HandleHelp()
    {
        ShowHelp();
        return CommandDispatchResult.Handled();
    }

    private static CommandDispatchResult HandleClear()
    {
        AnsiConsole.Clear();
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleNewSessionAsync(ReplState state)
    {
        state.Session = await state.Runtime.StartSessionAsync(new StartSessionRequest(state.AgentId, state.CurrentThreadSpace.ThreadSpaceId));
        state.SessionId = state.Session.Record.SessionId;
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[green]New session started.[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleResumeAsync(ReplState state)
    {
        var availableSessions = await state.Kernel.ThreadSpaces.ListSessionsAsync(state.CurrentThreadSpace.ThreadSpaceId);
        var lastSession = availableSessions
            .Where(s => s.SessionId != state.SessionId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (lastSession is null)
        {
            AnsiConsole.MarkupLine("[yellow]No previous session found to resume.[/]");
            return CommandDispatchResult.Handled();
        }

        await ActivateSessionAsync(state, lastSession);
        AnsiConsole.MarkupLine($"[green]Resumed session started at {lastSession.StartedAt:g}[/]");
        await ReplayRecentHistoryAsync(state.Runtime, state.SessionId);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleSessionsAsync(ReplState state, string arguments)
    {
        var sessions = await state.Kernel.ThreadSpaces.ListSessionsAsync(state.CurrentThreadSpace.ThreadSpaceId);
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No sessions found in this space.[/]");
            return CommandDispatchResult.Handled();
        }

        var orderedSessions = sessions
            .OrderByDescending(x => x.StartedAt)
            .ToArray();

        if (string.IsNullOrWhiteSpace(arguments))
        {
            await ShowSessionsTableAsync(state, orderedSessions);
            return CommandDispatchResult.Handled();
        }

        if (!int.TryParse(arguments, out var selectedIndex) || selectedIndex < 1 || selectedIndex > orderedSessions.Length)
        {
            AnsiConsole.MarkupLine($"[yellow]Session index must be between 1 and {orderedSessions.Length}.[/]");
            return CommandDispatchResult.Handled();
        }

        var selectedSession = orderedSessions[selectedIndex - 1];
        await ActivateSessionAsync(state, selectedSession);
        AnsiConsole.MarkupLine($"[green]Switched to session {selectedIndex}: {ShortId(selectedSession.SessionId)}[/]");
        await ReplayRecentHistoryAsync(state.Runtime, state.SessionId);
        return CommandDispatchResult.Handled();
    }

    private static async Task ShowSessionsTableAsync(ReplState state, IReadOnlyList<SessionRecord> sessions)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]#[/]");
        table.AddColumn("[yellow]Session ID[/]");
        table.AddColumn("[yellow]Agent[/]");
        table.AddColumn("[yellow]Started[/]");
        table.AddColumn("[yellow]Last Message[/]");

        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            var history = await state.Kernel.History.ListAsync(session.SessionId);
            var preview = history.LastOrDefault()?.Content ?? "(No messages yet)";
            if (preview.Length > 48)
            {
                preview = preview[..45] + "...";
            }

            table.AddRow(
                (index + 1).ToString(),
                ShortId(session.SessionId),
                session.AgentId,
                session.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                preview.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    private static async Task<CommandDispatchResult> HandleToolsAsync(ReplState state)
    {
        var launchPlan = await state.Runtime.PrepareAgentAsync(new AgentLaunchRequest(state.Session.Record.AgentId, state.SessionId));
        if (launchPlan.Tools.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No authorized tools are available for this session.[/]");
            return CommandDispatchResult.Handled();
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Tool[/]");
        table.AddColumn("[yellow]Description[/]");
        table.AddColumn("[yellow]Permission Status[/]");

        foreach (var tool in launchPlan.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            var capability = tool.Capabilities == ClawSharp.Lib.Tools.ToolCapability.None
                ? "No extra capability"
                : tool.Capabilities.ToString();
            var status = launchPlan.Session.EffectivePermissions?.ApprovalRequired == true
                ? $"Authorized ({capability}, approval required)"
                : $"Authorized ({capability})";

            table.AddRow(tool.Name, tool.Description, status);
        }

        AnsiConsole.Write(table);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleThreadSpaceSwitchAsync(ReplState state, string command, string arguments)
    {
        if (command == "/home" || string.IsNullOrWhiteSpace(arguments))
        {
            state.CurrentThreadSpace = await state.Kernel.ThreadSpaces.GetGlobalAsync();
        }
        else
        {
            var path = Path.GetFullPath(arguments);
            if (!Directory.Exists(path))
            {
                AnsiConsole.MarkupLine($"[red]Directory not found: {path.EscapeMarkup()}[/]");
                return CommandDispatchResult.Handled();
            }

            state.CurrentThreadSpace = await state.Kernel.ThreadSpaces.GetByBoundFolderPathAsync(path)
                ?? await state.Kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(Path.GetFileName(path), path));
        }

        state.Session = await state.Runtime.StartSessionAsync(new StartSessionRequest(state.AgentId, state.CurrentThreadSpace.ThreadSpaceId));
        state.SessionId = state.Session.Record.SessionId;
        AnsiConsole.MarkupLine($"[bold blue]Switched to space:[/] [green]{state.CurrentThreadSpace.Name.EscapeMarkup()}[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleInitAsync(ReplState state)
    {
        var targetDir = state.CurrentThreadSpace.BoundFolderPath ?? Directory.GetCurrentDirectory();
        var agentFile = Path.Combine(targetDir, "agent.md");

        if (File.Exists(agentFile))
        {
            AnsiConsole.MarkupLine($"[yellow]File already exists: {agentFile.EscapeMarkup()}[/]");
            return CommandDispatchResult.Handled();
        }

        var templatePath = Path.Combine(state.Options.Runtime.WorkspaceRoot, ".specify/templates/agent-file-template.md");
        var template = File.Exists(templatePath)
            ? await File.ReadAllTextAsync(templatePath)
            : "---\nid: my-agent\nname: My Agent\n---\n\nHello, I am your new agent.";

        await File.WriteAllTextAsync(agentFile, template);
        AnsiConsole.MarkupLine($"[green]Created agent definition:[/] [blue]{agentFile.EscapeMarkup()}[/]");
        await state.Runtime.InitializeAsync();
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleInitProjectAsync(ReplState state)
    {
        var templates = await state.Kernel.Projects.ListTemplatesAsync();
        if (templates.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No project templates found.[/]");
            return CommandDispatchResult.Handled();
        }

        var selectedTemplate = AnsiConsole.Prompt(
            new SelectionPrompt<ProjectTemplateDefinition>()
                .Title("Select a project template:")
                .AddChoices(templates)
                .UseConverter(t => $"{t.Name} ({t.Id})"));

        var projectName = AnsiConsole.Ask<string>("Enter project name:", "my-new-project");
        var targetDir = state.CurrentThreadSpace.BoundFolderPath ?? Directory.GetCurrentDirectory();
        var projectPath = Path.Combine(targetDir, projectName);

        var request = new CreateProjectRequest(
            selectedTemplate.Id,
            projectName,
            projectPath,
            new Dictionary<string, string>
            {
                ["ProjectName"] = projectName,
                ["Author"] = Environment.UserName,
                ["Date"] = DateTime.Now.ToString("yyyy-MM-dd")
            });

        var result = await state.Kernel.Projects.CreateProjectAsync(request);
        if (result is { IsSuccess: true, Value: { } projectResult })
        {
            AnsiConsole.MarkupLine($"[green]Project created successfully at:[/] [blue]{projectResult.ProjectRootPath.EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine("[grey]Type /cd <path> to switch to the new project space.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to create project: {result.Error?.EscapeMarkup() ?? "Unknown error"}[/]");
        }

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandlePasteAsync()
    {
        AnsiConsole.MarkupLine("[grey]Paste mode: enter content, then submit with a single '.' line.[/]");
        var pastedContent = await MultilineInputCollector.CapturePasteAsync(ReadPasteLineAsync, "[bold magenta]Paste[/] > ");
        return string.IsNullOrWhiteSpace(pastedContent)
            ? CommandDispatchResult.Handled()
            : CommandDispatchResult.Submit(pastedContent);
    }

    private static async Task<CommandDispatchResult> HandleEditAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clawsharp-edit-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempFile, string.Empty);

        try
        {
            var editor = Environment.GetEnvironmentVariable("EDITOR");
            if (string.IsNullOrWhiteSpace(editor))
            {
                editor = OperatingSystem.IsWindows() ? "notepad" : "vi";
            }

            using var process = Process.Start(CreateEditorStartInfo(editor, tempFile))
                ?? throw new InvalidOperationException($"Failed to start editor '{editor}'.");
            await process.WaitForExitAsync();

            var editedContent = await File.ReadAllTextAsync(tempFile);
            if (string.IsNullOrWhiteSpace(editedContent))
            {
                AnsiConsole.MarkupLine("[yellow]Editor closed without any content to send.[/]");
                return CommandDispatchResult.Handled();
            }

            return CommandDispatchResult.Submit(editedContent.Trim());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    internal static ProcessStartInfo CreateEditorStartInfo(string editorCommand, string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo("cmd.exe", $"/c {editorCommand} \"{filePath}\"")
            {
                UseShellExecute = false
            };
        }

        return new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            ArgumentList = { "-lc", $"{editorCommand} {EscapeShellArgument(filePath)}" }
        };
    }

    private static async Task<string?> ReadPasteLineAsync(string promptMarkup)
    {
        AnsiConsole.Markup(promptMarkup);
        return await Task.FromResult(Console.ReadLine());
    }

    private static string EscapeShellArgument(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private static CommandDispatchResult HandleUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[yellow]Unknown command: {command}. Type /help to see available commands.[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task ActivateSessionAsync(ReplState state, SessionRecord record)
    {
        state.Session = await state.Kernel.Sessions.GetAsync(record.SessionId);
        state.SessionId = record.SessionId;
        state.AgentId = record.AgentId;
    }

    private static async Task ReplayRecentHistoryAsync(IClawRuntime runtime, SessionId sessionId)
    {
        var history = await runtime.GetHistoryAsync(sessionId);
        var recentMessages = history
            .Where(entry => entry.Message is not null)
            .Select(entry => entry.Message!)
            .TakeLast(5)
            .ToArray();

        if (recentMessages.Length == 0)
        {
            AnsiConsole.MarkupLine("[grey]No messages found in this session yet.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[grey]Last few messages:[/]");
        foreach (var message in recentMessages)
        {
            var roleColor = message.Role switch
            {
                PromptMessageRole.User => "green",
                PromptMessageRole.Assistant => "blue",
                PromptMessageRole.Tool => "yellow",
                _ => "grey"
            };

            var summary = message.Content.Length > 80 ? message.Content[..77] + "..." : message.Content;
            AnsiConsole.MarkupLine($"[{roleColor}]- {message.Role}:[/] {summary.EscapeMarkup()}");
        }
    }

    private static async Task RunTurnAsync(ReplState state, string input)
    {
        await state.Runtime.AppendUserMessageAsync(state.SessionId, input);

        AnsiConsole.Markup("[bold yellow]Agent >[/] ");
        var hasTextOutput = false;
        string? finalAssistantMessage = null;

        try
        {
            await foreach (var @event in state.Runtime.RunTurnStreamingAsync(state.SessionId))
            {
                if (@event.Delta is not null)
                {
                    hasTextOutput = true;
                    Console.Write(@event.Delta);
                }

                if (@event.FinalResult is not null)
                {
                    finalAssistantMessage = @event.FinalResult.AssistantMessage;
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Error during turn execution:[/] {ex.Message.EscapeMarkup()}");
            hasTextOutput = true;
        }

        if (!hasTextOutput)
        {
            AnsiConsole.MarkupLine("[grey](No text response from agent)[/]");
        }
        else if (MarkdownCodeFenceDetector.ContainsFencedCodeBlock(finalAssistantMessage ?? string.Empty))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Rendered Markdown:[/]");
            AnsiConsole.Write(new Markdown(finalAssistantMessage ?? string.Empty));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private static void ShowWelcomeHeader(string agentId, string threadSpaceName)
    {
        var grid = new Grid().AddColumn();
        grid.AddRow(new Text("ClawSharp v1.0.0", new Style(Color.Blue, decoration: Decoration.Bold)));
        grid.AddRow(new Markup($"[grey]Agent:[/] [green]{agentId.EscapeMarkup()}[/]   [grey]ThreadSpace:[/] [blue]{threadSpaceName.EscapeMarkup()}[/]"));
        grid.AddRow(new Text("Type /help for commands", new Style(Color.Grey)));

        var panel = new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
        AnsiConsole.Write(panel);
    }

    private static string GetPrompt(ThreadSpaceRecord space)
    {
        var name = space.Name;
        if (name.Length > 20)
        {
            name = name[..17] + "...";
        }

        var color = space.IsGlobal ? "bold blue" : "cyan";
        return $"[{color}]{name.EscapeMarkup()}[/] > ";
    }

    private static void ShowHelp()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Command[/]");
        table.AddColumn("[yellow]Description[/]");
        table.AddRow("/help", "Show this help message");
        table.AddRow("/new", "Start a new session in current space");
        table.AddRow("/resume", "Resume last session in current space");
        table.AddRow("/sessions", "List sessions in the current space");
        table.AddRow("/sessions <index>", "Switch to a session and replay recent history");
        table.AddRow("/tools", "List currently authorized tools");
        table.AddRow("/paste", "Enter multiline paste mode and submit with '.'");
        table.AddRow("/edit", "Compose a prompt in your external editor");
        table.AddRow("/cd <path>", "Switch to a directory-bound space");
        table.AddRow("/home", "Switch back to global space");
        table.AddRow("/clear", "Clear terminal screen");
        table.AddRow("/init", "Initialize an agent definition (agent.md) in current space");
        table.AddRow("/init-proj", "Scaffold a new project from templates");
        table.AddRow("/quit, /exit", "Exit the REPL");
        AnsiConsole.Write(table);
    }

    private static string ShortId(SessionId sessionId) =>
        sessionId.Value.Length <= 8 ? sessionId.Value : sessionId.Value[..8];

    private sealed class ReplState(IClawRuntime runtime, IClawKernel kernel, ClawOptions options)
    {
        public IClawRuntime Runtime { get; } = runtime;
        public IClawKernel Kernel { get; } = kernel;
        public ClawOptions Options { get; } = options;
        public required string AgentId { get; set; }
        public required ThreadSpaceRecord CurrentThreadSpace { get; set; }
        public required RuntimeSession Session { get; set; }
        public required SessionId SessionId { get; set; }
        public required ReplPrompt PromptHandler { get; set; }
    }

    private sealed record CommandDispatchResult(bool ExitRequested, string? SubmittedInput = null)
    {
        public static CommandDispatchResult Handled() => new(false);

        public static CommandDispatchResult Submit(string value) => new(false, value);

        public static CommandDispatchResult Exit() => new(true);
    }
}
