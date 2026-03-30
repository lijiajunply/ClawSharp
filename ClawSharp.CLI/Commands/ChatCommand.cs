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

            AnsiConsole.Write(new Rule("[bold blue]ClawSharp REPL[/]") { Justification = Justify.Left });
            AnsiConsole.MarkupLine($"[grey]Agent:[/] [green]{finalAgentId.EscapeMarkup()}[/] | [grey]Space:[/] [blue]{currentThreadSpace.Name.EscapeMarkup()}[/]");
            AnsiConsole.MarkupLine("[grey]Type '/help' for available commands, '/quit' to exit.[/]");
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
                            AnsiConsole.MarkupLine("[yellow]Started a new session.[/]");
                            continue;
                        case "/resume":
                            {
                                var sessionsInSpace = await kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpace.ThreadSpaceId);
                                var lastSession = sessionsInSpace.OrderByDescending(s => s.StartedAt).FirstOrDefault();
                                if (lastSession != null)
                                {
                                    session = await kernel.Sessions.GetAsync(lastSession.SessionId);
                                    sessionId = session.Record.SessionId;
                                    AnsiConsole.MarkupLine($"[green]Resumed session from {lastSession.StartedAt:g}[/]");                                    // Optional: Print last few messages
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[red]No previous session found to resume.[/]");
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
                                    var path = Path.GetFullPath(Path.IsPathRooted(cmdArgs) ? cmdArgs : Path.Combine(options.Runtime.WorkspaceRoot, cmdArgs));
                                    currentThreadSpace = await kernel.ThreadSpaces.GetByBoundFolderPathAsync(path) 
                                                         ?? await kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(Path.GetFileName(path), path));
                                }
                                
                                session = await runtime.StartSessionAsync(new StartSessionRequest(finalAgentId, currentThreadSpace.ThreadSpaceId));
                                sessionId = session.Record.SessionId;
                                AnsiConsole.MarkupLine($"[bold blue]Switched to:[/] [green]{currentThreadSpace.Name.EscapeMarkup()}[/]");
                            }
                            continue;
                        default:
                            AnsiConsole.MarkupLine($"[red]Unknown command: {cmd}. Type /help for assistance.[/]");
                            continue;
                    }
                }

                await runtime.AppendUserMessageAsync(sessionId, trimmedInput);
                AnsiConsole.Markup($"{ThemeConfig.AgentPrefix} ");

                var responseBuilder = new StringBuilder();
                var hasTextOutput = false;
                var markdown = new Markdown("");

                await AnsiConsole.Live(markdown)
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        await foreach (var @event in runtime.RunTurnStreamingAsync(sessionId))
                        {
                            if (@event.Delta != null)
                            {
                                hasTextOutput = true;
                                responseBuilder.Append(@event.Delta);
                                markdown.Update(responseBuilder.ToString());
                                ctx.Refresh();
                            }
                        }
                    });

                if (!hasTextOutput)
                {
                    AnsiConsole.MarkupLine("[grey](No text response from agent)[/]");
                }
                AnsiConsole.WriteLine();
            }

            return 0;
        });
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
        table.AddRow("/quit", "Exit the REPL");
        AnsiConsole.Write(table);
    }
}
