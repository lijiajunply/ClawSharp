using System.CommandLine;
using System.Text;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
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

            // Initialize or switch to Global ThreadSpace by default
            var currentThreadSpace = await kernel.ThreadSpaces.GetGlobalAsync();
            var finalAgentId = agentId;
            
            if (string.IsNullOrWhiteSpace(finalAgentId))
            {
                finalAgentId = options.Agents.DefaultAgentId;
            }

            if (string.IsNullOrWhiteSpace(finalAgentId))
            {
                var firstAgent = kernel.Agents.GetAll().FirstOrDefault();
                finalAgentId = firstAgent?.Id;
            }

            if (string.IsNullOrWhiteSpace(finalAgentId))
            {
                throw new InvalidOperationException("No agents found in the registry and no default agent is configured.");
            }

            // Start session in Global TS
            var session = await runtime.StartSessionAsync(new StartSessionRequest(finalAgentId, currentThreadSpace.ThreadSpaceId));
            var sessionId = session.Record.SessionId;

            ShowWelcomeHeader(finalAgentId, currentThreadSpace.Name);
            
            var sessionsInSpace = await kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpace.ThreadSpaceId);
            if (sessionsInSpace.Count > 1)
            {
                AnsiConsole.MarkupLine("[grey]Tip: type /resume to continue last conversation.[/]");
            }
            AnsiConsole.WriteLine();

            while (true)
            {
                var prompt = GetPrompt(currentThreadSpace);
                var input = AnsiConsole.Ask<string>(prompt);
                if (input == null!) break;

                var trimmedInput = input.Trim();
                if (string.IsNullOrWhiteSpace(trimmedInput)) continue;

                if (trimmedInput.StartsWith("/"))
                {
                    var parts = trimmedInput.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var cmd = parts[0].ToLowerInvariant();
                    var cmdArgs = parts.Length > 1 ? parts[1] : string.Empty;

                    if (cmd == "/exit" || cmd == "/quit") break;

                    switch (cmd)
                    {
                        case "/help":
                            ShowHelp();
                            continue;
                        case "/clear":
                            AnsiConsole.Clear();
                            continue;
                        case "/new":
                            session = await runtime.StartSessionAsync(new StartSessionRequest(finalAgentId, currentThreadSpace.ThreadSpaceId));
                            sessionId = session.Record.SessionId;
                            AnsiConsole.Clear();
                            AnsiConsole.MarkupLine("[green]New session started.[/]");
                            continue;
                        case "/resume":
                            {
                                var availableSessions = await kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpace.ThreadSpaceId);
                                var lastSession = availableSessions
                                    .Where(s => s.SessionId != sessionId)
                                    .OrderByDescending(s => s.StartedAt)
                                    .FirstOrDefault();

                                if (lastSession != null)
                                {
                                    session = await kernel.Sessions.GetAsync(lastSession.SessionId);
                                    sessionId = session.Record.SessionId;
                                    AnsiConsole.MarkupLine($"[green]Resumed session started at {lastSession.StartedAt:g}[/]");
                                    
                                    var history = await kernel.History.ListAsync(sessionId);
                                    if (history.Count > 0)
                                    {
                                        AnsiConsole.MarkupLine("[grey]Last few messages:[/]");
                                        foreach (var msg in history.TakeLast(5))
                                        {
                                            var summary = msg.Content.Length > 80 ? msg.Content[..77] + "..." : msg.Content;
                                            AnsiConsole.MarkupLine($"[grey]- {msg.Role}: {summary.EscapeMarkup()}[/]");
                                        }
                                    }
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[yellow]No previous session found to resume.[/]");
                                }
                            }
                            continue;
                        case "/cd":
                        case "/home":
                            {
                                if (cmd == "/home" || string.IsNullOrWhiteSpace(cmdArgs))
                                {
                                    currentThreadSpace = await kernel.ThreadSpaces.GetGlobalAsync();
                                }
                                else
                                {
                                    var path = Path.GetFullPath(cmdArgs);
                                    if (!Directory.Exists(path))
                                    {
                                        AnsiConsole.MarkupLine($"[red]Directory not found: {path}[/]");
                                        continue;
                                    }

                                    currentThreadSpace = await kernel.ThreadSpaces.GetByBoundFolderPathAsync(path) 
                                                         ?? await kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(Path.GetFileName(path), path));
                                }
                                
                                session = await runtime.StartSessionAsync(new StartSessionRequest(finalAgentId, currentThreadSpace.ThreadSpaceId));
                                sessionId = session.Record.SessionId;
                                AnsiConsole.MarkupLine($"[bold blue]Switched to space:[/] [green]{currentThreadSpace.Name.EscapeMarkup()}[/]");
                            }
                            continue;
                        default:
                            AnsiConsole.MarkupLine($"[yellow]Unknown command: {cmd}. Type /help to see available commands.[/]");
                            continue;
                    }
                }

                await runtime.AppendUserMessageAsync(sessionId, trimmedInput);
                
                AnsiConsole.Markup("[bold yellow]Agent >[/] ");
                var hasTextOutput = false;

                await foreach (var @event in runtime.RunTurnStreamingAsync(sessionId))
                {
                    if (@event.Delta != null)
                    {
                        hasTextOutput = true;
                        // Use standard Console for high-performance delta streaming to avoid cursor glitches
                        Console.Write(@event.Delta);
                    }
                }

                if (!hasTextOutput)
                {
                    AnsiConsole.MarkupLine("[grey](No text response from agent)[/]");
                }
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine(); // Add extra spacing between turns
            }

            return 0;
        });
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
        table.AddRow("/cd <path>", "Switch to a directory-bound space");
        table.AddRow("/home", "Switch back to global space");
        table.AddRow("/clear", "Clear terminal screen");
        table.AddRow("/quit, /exit", "Exit the REPL");
        AnsiConsole.Write(table);
    }
}
