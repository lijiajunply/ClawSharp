using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ChatCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("chat", "Start a new REPL session with the specified agent");
        var agentIdArg = new Argument<string>("agent-id", "The ID of the agent to chat with");
        command.AddArgument(agentIdArg);

        command.SetHandler(async (agentId) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                AnsiConsole.MarkupLine($"[bold blue]Starting session with agent:[/] [green]{agentId}[/]");
                var session = await runtime.StartSessionAsync(agentId);
                var sessionId = session.Record.SessionId;

                AnsiConsole.MarkupLine("[grey]Type '/exit' or '/quit' to leave the session.[/]");

                while (true)
                {
                    var input = AnsiConsole.Ask<string>("[bold green]User >[/]");

                    if (string.Equals(input, "/exit", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(input, "/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    await runtime.AppendUserMessageAsync(sessionId, input);

                    await AnsiConsole.Status()
                        .StartAsync("[bold blue]Agent is thinking...[/]", async ctx =>
                        {
                            var result = await runtime.RunTurnAsync(sessionId);
                            AnsiConsole.MarkupLine("[bold blue]Agent > [/]");
                            AnsiConsole.WriteLine(result.AssistantMessage);
                        });
                }

                return 0;
            });
        }, agentIdArg);

        return command;
    }
}
