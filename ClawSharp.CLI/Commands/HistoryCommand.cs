using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class HistoryCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("history", I18n.T("History.Description"));
        var sessionIdArg = new Argument<string>("session-id", I18n.T("History.SessionIdArg"));
        command.AddArgument(sessionIdArg);
        
        command.SetHandler(async sessionIdValue =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var sessionId = new SessionId(sessionIdValue);
                var history = await runtime.GetHistoryAsync(sessionId);

                foreach (var entry in history.OrderBy(m => m.SequenceNo))
                {
                    if (entry.Message == null) continue;
                    
                    var message = entry.Message;
                    var panel = new Panel(new Markdown(message.Content));
                    panel.Header = new PanelHeader(message.Role.ToString());
                    
                    if (message.Role == PromptMessageRole.User)
                    {
                        panel.BorderColor(Color.Green);
                        AnsiConsole.MarkupLine(I18n.T("History.User"));
                    }
                    else if (message.Role == PromptMessageRole.Assistant)
                    {
                        panel.BorderColor(Color.Blue);
                        AnsiConsole.MarkupLine(I18n.T("History.Agent"));
                    }
                    else
                    {
                        panel.BorderColor(Color.Grey);
                        AnsiConsole.MarkupLine($"[grey]{message.Role}:[/]");
                    }

                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();
                }

                return 0;
            });
        }, sessionIdArg);

        return command;
    }
}
