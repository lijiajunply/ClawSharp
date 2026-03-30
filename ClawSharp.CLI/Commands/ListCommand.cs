using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ListCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("list", "List sessions in the default ThreadSpace");

        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var globalSpace = await kernel.ThreadSpaces.GetGlobalAsync();
                var sessions = await kernel.ThreadSpaces.ListSessionsAsync(globalSpace.ThreadSpaceId);

                var table = new Table();
                table.AddColumn("Session ID");
                table.AddColumn("Agent ID");
                table.AddColumn("Started At");
                table.AddColumn("Status");

                foreach (var session in sessions.OrderByDescending(s => s.StartedAt))
                {
                    table.AddRow(
                        session.SessionId.Value,
                        session.AgentId,
                        session.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        session.Status.ToString());
                }

                AnsiConsole.Write(table);
                return 0;
            });
        });

        return command;
    }
}
