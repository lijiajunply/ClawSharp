using System.CommandLine;
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

        command.SetHandler(async (agentId) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var options = host.Services.GetRequiredService<ClawOptions>();

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

                AnsiConsole.MarkupLine($"[bold blue]Starting session with agent:[/] [green]{finalAgentId.EscapeMarkup()}[/]");
                var session = await runtime.StartSessionAsync(finalAgentId);
                var sessionId = session.Record.SessionId;

                AnsiConsole.MarkupLine("[grey]Type '/exit' or '/quit' to leave the session.[/]");

                while (true)
                {
                    var input = AnsiConsole.Ask<string>($"{ThemeConfig.UserPrefix} ");
                    if (input == null) break;
                    
                    var trimmedInput = input.Trim();

                    if (string.Equals(trimmedInput, "/exit", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(trimmedInput, "/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    await runtime.AppendUserMessageAsync(sessionId, trimmedInput);

                    AnsiConsole.Markup($"{ThemeConfig.AgentPrefix} ");

                    var hasOutput = false;
                    await foreach (var @event in runtime.RunTurnStreamingAsync(sessionId))
                    {
                        if (@event.Delta != null)
                        {
                            hasOutput = true;
                            Console.Write(@event.Delta);
                        }
                    }
                    
                    if (!hasOutput)
                    {
                        AnsiConsole.MarkupLine("[grey](No text response from agent)[/]");
                    }
                    
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine();
                }

                return 0;
            });
        }, agentIdArg);

        return command;
    }
}
